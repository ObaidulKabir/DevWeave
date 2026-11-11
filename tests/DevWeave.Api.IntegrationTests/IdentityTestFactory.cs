using DevWeave.Application.Abstractions;
using DevWeave.Api.Security;
using DevWeave.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using DevWeave.Infrastructure.Security;
using DevWeave.Domain.Identity;
using DevWeave.Contracts.Identity.V1;
using DevWeave.Contracts.Projects.V1;
using DevWeave.Contracts.Lifecycle.V1;
using System.Text;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace DevWeave.Api.IntegrationTests;

/// <summary>
/// Boots the Api against a unique Postgres test database (created on demand and dropped on dispose)
/// so the negative-disclosure suite runs against a real database rather than a stand-in. The connection
/// string is injected through the host configuration so parallel fixtures never share global state.
/// </summary>
public sealed class IdentityTestFactory : WebApplicationFactory<Program>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };

    public const string TestOidcIssuer = "https://identity.devweave.test";
    public const string TestOidcAudience = "devweave-integration-tests";
    private const string TestOidcSigningKey = "devweave-oidc-integration-test-key-32bytes";
    private const string TestAgentSigningKey = "devweave-agent-integration-test-key-32bytes";

    private readonly string _databaseName;
    private readonly string _adminConnectionString;
    private readonly bool _enforceTenantMembership;
    private bool _prepared;

    public IdentityTestFactory()
        : this(enforceTenantMembership: false)
    {
    }

    internal IdentityTestFactory(bool enforceTenantMembership)
    {
        _enforceTenantMembership = enforceTenantMembership;
        _databaseName = $"devweave_it_{Guid.NewGuid():N}";
        _adminConnectionString = BuildConnectionString("postgres");
        ConnectionString = BuildConnectionString(_databaseName);
    }

    public string ConnectionString { get; }

    private static string BuildConnectionString(string database)
    {
        // CI (and any other sandbox) points the suite at an isolated PostgreSQL instance via these
        // environment variables; when absent we fall back to the local docker-compose service on host
        // port 5433. The unique per-fixture database name is still chosen locally to keep fixtures
        // isolated even when many run in parallel against one shared server.
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = Environment.GetEnvironmentVariable("DEVWEAVE_TEST_DB_HOST") ?? "localhost",
            Port = int.TryParse(Environment.GetEnvironmentVariable("DEVWEAVE_TEST_DB_PORT"), out var port) ? port : 5433,
            Database = database,
            Username = Environment.GetEnvironmentVariable("DEVWEAVE_TEST_DB_USER") ?? "devweave",
            Password = Environment.GetEnvironmentVariable("DEVWEAVE_TEST_DB_PASSWORD") ?? "change-me-locally",
        };

        return builder.ConnectionString;
    }

    public void PrepareDatabase()
    {
        if (_prepared)
        {
            return;
        }

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DevWeaveDbContext>();

        var creator = db.GetService<IRelationalDatabaseCreator>();
        if (!creator.Exists())
        {
            creator.Create();
        }

        db.Database.Migrate();
        _prepared = true;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DevWeave"] = ConnectionString,
                ["Auth:Oidc:Authority"] = "",
                ["Auth:Oidc:Audience"] = TestOidcAudience,
                ["Auth:SigningKey"] = TestAgentSigningKey,
                ["Authorization:RequireTenantMembership"] = _enforceTenantMembership.ToString(),
            });
        });

        // Swap the S3/MinIO adapters for in-memory fakes so artifact round-trips exercise the real
        // application + endpoint pipeline without requiring an external object store.
        builder.ConfigureTestServices(services =>
        {
            services
                .AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestTenantAuthenticationHandler>(
                    TestTenantAuthenticationHandler.SchemeName,
                    _ => { });
            services.PostConfigure<PolicySchemeOptions>(ApiAuthenticationSchemes.SmartBearer, options =>
            {
                var productionSelector = options.ForwardDefaultSelector;
                options.ForwardDefaultSelector = context =>
                {
                    if (context.Request.Headers.ContainsKey("X-Test-Anonymous"))
                        return ApiAuthenticationSchemes.AgentBearer;

                    var authorization = context.Request.Headers.Authorization.ToString();
                    if (string.IsNullOrWhiteSpace(authorization))
                        return TestTenantAuthenticationHandler.SchemeName;

                    return productionSelector?.Invoke(context) ?? ApiAuthenticationSchemes.AgentBearer;
                };
            });

            services.PostConfigure<JwtBearerOptions>(ApiAuthenticationSchemes.HumanOidc, options =>
            {
                options.TokenValidationParameters.ValidateIssuer = true;
                options.TokenValidationParameters.ValidIssuer = TestOidcIssuer;
                options.TokenValidationParameters.ValidateAudience = true;
                options.TokenValidationParameters.ValidAudience = TestOidcAudience;
                options.TokenValidationParameters.ValidateIssuerSigningKey = true;
                options.TokenValidationParameters.IssuerSigningKey =
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestOidcSigningKey));
                options.TokenValidationParameters.ValidateLifetime = true;
            });

            services.AddSingleton<IObjectStorage, InMemoryObjectStorage>();
            services.AddSingleton<IQuarantineStore, InMemoryQuarantineStore>();
        });
    }

    public static string IssueTestOidcToken(
        Guid tenantId,
        Guid principalId,
        string principalType = "Human",
        string audience = TestOidcAudience,
        bool includeIdentityClaims = true,
        bool isPlatformAdmin = false)
    {
        var claims = new List<System.Security.Claims.Claim>
        {
            new("sub", Guid.NewGuid().ToString("N")),
        };

        if (includeIdentityClaims)
        {
            claims.Add(new System.Security.Claims.Claim(AuthClaims.TenantId, tenantId.ToString("D")));
            claims.Add(new System.Security.Claims.Claim(AuthClaims.PrincipalId, principalId.ToString("D")));
            claims.Add(new System.Security.Claims.Claim(AuthClaims.PrincipalType, principalType));
            
            if (isPlatformAdmin)
            {
                claims.Add(new System.Security.Claims.Claim("platform_admin", "true"));
            }
        }

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestOidcSigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            TestOidcIssuer,
            audience,
            claims,
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(5),
            credentials);

        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }

    public HttpClient CreateAuthenticatedHumanClient(Guid tenantId, Guid principalId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                IssueTestOidcToken(tenantId, principalId));
        return client;
    }

    public HttpClient CreateAuthenticatedAgentClient(Guid tenantId, Guid principalId)
    {
        var client = CreateClient();
        var service = new HmacAccessTokenService(TestAgentSigningKey);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                service.Issue(new TenantId(tenantId), new PrincipalId(principalId), PrincipalType.AiAgent, TimeSpan.FromHours(1)));
        return client;
    }

    public HttpClient CreatePlatformAdminClient(Guid tenantId, Guid principalId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                IssueTestOidcToken(tenantId, principalId, isPlatformAdmin: true));
        return client;
    }

    public async Task<(HttpClient Client, TenantResponse Tenant, PrincipalResponse User)> CreateAuthenticatedHumanClientAsync()
    {
        PrepareDatabase();
        var client = CreateClient();
        
        // 1. Create Tenant
        var tenantResponse = await client.PostAsJsonAsync("/tenants", new CreateTenantRequest("Test Tenant"), JsonOptions);
        tenantResponse.EnsureSuccessStatusCode();
        var tenant = (await tenantResponse.Content.ReadFromJsonAsync<TenantResponse>(JsonOptions))!;

        // 2. Create Human Principal
        var userResponse = await client.PostAsJsonAsync($"/tenants/{tenant.Id}/principals", new CreatePrincipalRequest("user@test.com", PrincipalType.Human), JsonOptions);
        userResponse.EnsureSuccessStatusCode();
        var user = (await userResponse.Content.ReadFromJsonAsync<PrincipalResponse>(JsonOptions))!;

        // 3. Issue Token
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", IssueTestOidcToken(tenant.Id, user.Id));

        return (client, tenant, user);
    }

    public async Task<ProjectResponse> CreateProjectAsync(Guid tenantId)
    {
        using var client = CreateClient();
        // 1. Create a human principal to be the creator
        var adminResponse = await client.PostAsJsonAsync($"/tenants/{tenantId}/principals", new CreatePrincipalRequest("admin@test.com", PrincipalType.Human), JsonOptions);
        adminResponse.EnsureSuccessStatusCode();
        var admin = (await adminResponse.Content.ReadFromJsonAsync<PrincipalResponse>(JsonOptions))!;
        
        // Use an admin token for setup
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", IssueTestOidcToken(tenantId, admin.Id));

        // 2. Create a lifecycle profile
        var profileResponse = await client.PostAsJsonAsync($"/tenants/{tenantId}/lifecycle-profiles", new CreateLifecycleProfileRequest("default", "Default Profile", "1.0"), JsonOptions);
        profileResponse.EnsureSuccessStatusCode();
        var profile = (await profileResponse.Content.ReadFromJsonAsync<LifecycleProfileResponse>(JsonOptions))!;
        
        // 3. Create Project
        var response = await client.PostAsJsonAsync($"/tenants/{tenantId}/projects", new CreateProjectRequest("Test Project", "TP", "Description", profile.Id, admin.Id), JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions))!;
    }

    public async Task<PrincipalResponse> CreateAgentAsync(Guid tenantId, Guid projectId)
    {
        using var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", IssueTestOidcToken(tenantId, Guid.NewGuid()));

        var response = await client.PostAsJsonAsync($"/tenants/{tenantId}/principals", new CreatePrincipalRequest("agent@test.com", PrincipalType.AiAgent), JsonOptions);
        response.EnsureSuccessStatusCode();
        var agent = (await response.Content.ReadFromJsonAsync<PrincipalResponse>(JsonOptions))!;

        // Grant access to project
        await client.PostAsJsonAsync($"/tenants/{tenantId}/members", new InviteMemberRequest(agent.Id, MembershipRole.Member), JsonOptions);

        return agent;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DropDatabase();
        }

        base.Dispose(disposing);
    }

    private void DropDatabase()
    {
        try
        {
            // PostgreSQL 18 supports WITH (FORCE) to terminate lingering pooled connections.
            using var connection = new NpgsqlConnection(_adminConnectionString);
            connection.Open();

            using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)", connection);
            command.ExecuteNonQuery();
        }
        catch
        {
            // Best-effort cleanup: database names are unique, so an orphaned database is harmless.
        }
    }
}
