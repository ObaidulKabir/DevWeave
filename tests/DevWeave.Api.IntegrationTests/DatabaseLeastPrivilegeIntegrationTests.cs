using Npgsql;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DevWeave.Api.IntegrationTests;

/// <summary>
/// Proves the database defense-in-depth controls introduced for H2 against a real PostgreSQL server:
/// a least-privilege application role that is subject to row-level security, composite ownership
/// constraints that make cross-tenant parent/child tuples unrepresentable, append-only history that
/// the ordinary role cannot rewrite, and pooled connections that do not leak a tenant session between
/// checkouts (architecture 15.2/16.2; TEN-002, DATA-03, REV-001/006).
/// </summary>
public sealed class DatabaseLeastPrivilegeIntegrationTests : IClassFixture<IdentityTestFactory>
{
    private const string ApplicationRole = "devweave_app";

    private readonly IdentityTestFactory _factory;

    public DatabaseLeastPrivilegeIntegrationTests(IdentityTestFactory factory)
    {
        _factory = factory;
        _factory.PrepareDatabase();
    }

    [Fact]
    public async Task TEN_002_row_level_security_filters_reads_to_the_session_tenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using var owner = OpenOwnerConnection();
        await SeedPrincipalAsync(owner, tenantA, "a@alphacorp.com", "Alpha Corp");
        await SeedPrincipalAsync(owner, tenantB, "b@betacorp.com", "Beta Corp");

        await using var app = OpenOwnerConnection();
        await ExecuteAsync(app, $"SET ROLE {ApplicationRole};");
        await ExecuteAsync(app, $"SET app.tenant_id = '{tenantA}';");

        Assert.Equal(new[] { "a@alphacorp.com" }, await QueryEmailsAsync(app));
    }

    [Fact]
    public async Task TEN_002_row_level_security_fails_closed_without_a_tenant_session()
    {
        var tenantA = Guid.NewGuid();

        await using var owner = OpenOwnerConnection();
        await SeedPrincipalAsync(owner, tenantA, "a@alphacorp.com", "Alpha Corp");

        // No tenant setting: the policy must match nothing rather than disclose every row.
        await using var app = OpenOwnerConnection();
        await ExecuteAsync(app, $"SET ROLE {ApplicationRole};");

        Assert.Empty(await QueryEmailsAsync(app));
    }

    [Fact]
    public async Task TEN_002_ef_connection_interceptor_applies_tenant_scope_for_the_application_role()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using (var owner = OpenOwnerConnection())
        {
            await SeedPrincipalAsync(owner, tenantA, "a@alphacorp.com", "Alpha Corp");
            await SeedPrincipalAsync(owner, tenantB, "b@betacorp.com", "Beta Corp");
        }

        var roleConnection = new NpgsqlConnectionStringBuilder(_factory.ConnectionString)
        {
            Pooling = false,
            Options = "-c role=devweave_app",
        }.ConnectionString;

        var scopedTenant = new TestTenantContext { TenantId = tenantA };
        var scopedOptions = new DbContextOptionsBuilder<DevWeaveDbContext>()
            .UseNpgsql(roleConnection)
            .AddInterceptors(new TenantSessionConnectionInterceptor(scopedTenant))
            .Options;

        await using (var scopedDb = new DevWeaveDbContext(scopedOptions))
        {
            var emails = await scopedDb.Principals.AsNoTracking()
                .Select(principal => principal.Email)
                .ToArrayAsync();
            Assert.Equal(new[] { "a@alphacorp.com" }, emails);
        }

        scopedTenant.TenantId = null;
        await using var unsetTenantDb = new DevWeaveDbContext(scopedOptions);
        Assert.Empty(await unsetTenantDb.Principals.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task TEN_002_row_level_security_blocks_writes_for_another_tenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using var owner = OpenOwnerConnection();
        await SeedPrincipalAsync(owner, tenantA, "a@alphacorp.com", "Alpha Corp");
        await SeedPrincipalAsync(owner, tenantB, "b@betacorp.com", "Beta Corp");

        await using var app = OpenOwnerConnection();
        await ExecuteAsync(app, $"SET ROLE {ApplicationRole};");
        await ExecuteAsync(app, $"SET app.tenant_id = '{tenantA}';");

        var violation = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            app,
            $"INSERT INTO principals (\"Id\", \"TenantId\", \"Email\", \"IsActive\") "
            + $"VALUES ('{Guid.NewGuid()}', '{tenantB}', 'intruder@betacorp.com', true);"));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, violation.SqlState);
    }

    [Fact]
    public async Task DATA_03_composite_ownership_rejects_a_parent_owned_by_another_tenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        await using var owner = OpenOwnerConnection();
        await SeedPrincipalAsync(owner, tenantA, "a@alphacorp.com", "Alpha Corp", principalId);
        await ExecuteAsync(owner,
            $"INSERT INTO tenants (\"Id\", \"Name\", \"CreatedAt\") VALUES ('{tenantB}', 'Beta Corp', now());");
        await ExecuteAsync(owner,
            "INSERT INTO lifecycle_profiles (\"Id\", \"TenantId\", \"Key\", \"Name\", \"Version\", \"CreatedAt\") "
            + $"VALUES ('{profileId}', '{tenantA}', 'agile', 'Agile', '1.0', now());");
        await ExecuteAsync(owner,
            "INSERT INTO projects (\"Id\", \"TenantId\", \"Name\", \"Key\", \"Status\", \"LifecycleProfileId\", "
            + $"\"CreatedBy\", \"CreatedAt\", \"IsDeleted\") VALUES ('{projectId}', '{tenantA}', 'shop-api', "
            + $"'shop-api', 0, '{profileId}', '{principalId}', now(), false);");

        // The item claims tenant B while naming a project owned by tenant A: the composite
        // (ProjectId, TenantId) key has no matching project tuple, so the row is rejected.
        var violation = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            owner,
            "INSERT INTO knowledge_items (\"Id\", \"TenantId\", \"ProjectId\", \"Key\", \"Title\", "
            + "\"Classification\", \"RepresentationKind\", \"State\", \"ReviewState\", \"VerificationState\", "
            + "\"CurrencyState\", \"TypeId\", \"SchemaVersion\", \"CurrentRevisionNumber\", \"CreatedBy\", \"CreatedAt\") "
            + $"VALUES ('{Guid.NewGuid()}', '{tenantB}', '{projectId}', 'spec', 'Spec', 0, 0, 0, 0, 0, 0, "
            + $"'devweave/requirement', '1.0', 0, '{principalId}', now());"));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, violation.SqlState);
    }

    [Fact]
    public async Task REV_001_application_role_cannot_rewrite_or_erase_history()
    {
        await using var app = OpenOwnerConnection();
        await ExecuteAsync(app, $"SET ROLE {ApplicationRole};");
        await ExecuteAsync(app, $"SET app.tenant_id = '{Guid.NewGuid()}';");

        var update = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            app, "UPDATE knowledge_revisions SET \"Reason\" = 'rewritten';"));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, update.SqlState);

        var delete = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(app, "DELETE FROM activities;"));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, delete.SqlState);
    }

    [Fact]
    public async Task TEN_002_pooled_connections_do_not_leak_the_tenant_session_setting()
    {
        var tenant = Guid.NewGuid();

        await using var dataSource = NpgsqlDataSource.Create(_factory.ConnectionString);

        int firstBackend;
        await using (var first = await dataSource.OpenConnectionAsync())
        {
            await ExecuteAsync(first, $"SET app.tenant_id = '{tenant}';");
            firstBackend = await QueryBackendPidAsync(first);
        }

        // The next checkout reuses the same physical backend; the tenant setting must have been reset.
        await using var second = await dataSource.OpenConnectionAsync();
        Assert.Equal(firstBackend, await QueryBackendPidAsync(second));
        Assert.True(string.IsNullOrEmpty(await QuerySettingAsync(second, "app.tenant_id")));
    }

    [Fact]
    public async Task LFC_005_work_planning_tables_are_tenant_scoped_and_transitions_are_append_only()
    {
        var tenant = Tenant.Create("Lifecycle RLS");
        var profile = LifecycleProfile.Create(tenant.Id, "default", "Default", "1.0");
        var actor = Principal.Create(tenant.Id, "lifecycle@example.test", PrincipalType.Human);
        var project = Project.Create(tenant.Id, "Lifecycle", "lifecycle", null, profile.Id, actor.Id);
        var item = WorkItem.Create(tenant.Id, project.Id, "DW-1", "Work", "Description", null,
            null, null, WorkPriority.Normal, actor.Id);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
            db.Tenants.Add(tenant);
            db.LifecycleProfiles.Add(profile);
            db.Principals.Add(actor);
            db.Projects.Add(project);
            db.WorkItems.Add(item);
            await db.SaveChangesAsync();
        }

        await using (var app = OpenOwnerConnection())
        {
            await ExecuteAsync(app, $"SET ROLE {ApplicationRole};");
            Assert.Equal(0, await QueryCountAsync(app, "SELECT count(*) FROM work_items;"));
            await ExecuteAsync(app, $"SET app.tenant_id = '{tenant.Id.Value}';");
            Assert.Equal(1, await QueryCountAsync(app,
                $"SELECT count(*) FROM work_items WHERE \"Id\" = '{item.Id.Value}';"));

            var update = await Assert.ThrowsAsync<PostgresException>(() =>
                ExecuteAsync(app, "UPDATE work_item_transitions SET \"Reason\" = 'rewritten';"));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, update.SqlState);
            var delete = await Assert.ThrowsAsync<PostgresException>(() =>
                ExecuteAsync(app, "DELETE FROM work_item_transitions;"));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, delete.SqlState);
        }
    }

    private NpgsqlConnection OpenOwnerConnection()
    {
        var builder = new NpgsqlConnectionStringBuilder(_factory.ConnectionString) { Pooling = false };
        var connection = new NpgsqlConnection(builder.ConnectionString);
        connection.Open();
        return connection;
    }

    private static async Task SeedPrincipalAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        string email,
        string tenantName,
        Guid? principalId = null)
    {
        await ExecuteAsync(connection,
            $"INSERT INTO tenants (\"Id\", \"Name\", \"CreatedAt\") VALUES ('{tenantId}', '{tenantName}', now()) "
            + "ON CONFLICT (\"Id\") DO NOTHING;");
        await ExecuteAsync(connection,
            "INSERT INTO principals (\"Id\", \"TenantId\", \"Email\", \"IsActive\") "
            + $"VALUES ('{principalId ?? Guid.NewGuid()}', '{tenantId}', '{email}', true);");
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<List<string>> QueryEmailsAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("SELECT \"Email\" FROM principals ORDER BY \"Email\";", connection);
        await using var reader = await command.ExecuteReaderAsync();

        var emails = new List<string>();
        while (await reader.ReadAsync())
        {
            emails.Add(reader.GetString(0));
        }

        return emails;
    }

    private static async Task<int> QueryBackendPidAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("SELECT pg_backend_pid();", connection);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<long> QueryCountAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<string?> QuerySettingAsync(NpgsqlConnection connection, string name)
    {
        await using var command = new NpgsqlCommand($"SELECT current_setting('{name}', true);", connection);
        return await command.ExecuteScalarAsync() as string;
    }

    private sealed class TestTenantContext : DevWeave.Application.Abstractions.ITenantContext
    {
        public Guid? TenantId { get; set; }
    }
}
