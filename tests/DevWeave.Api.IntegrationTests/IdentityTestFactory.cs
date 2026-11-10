using DevWeave.Application.Abstractions;
using DevWeave.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace DevWeave.Api.IntegrationTests;

/// <summary>
/// Boots the Api against a unique Postgres test database (created on demand and dropped on dispose)
/// so the negative-disclosure suite runs against a real database rather than a stand-in. The connection
/// string is injected through the host configuration so parallel fixtures never share global state.
/// </summary>
public sealed class IdentityTestFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName;
    private readonly string _adminConnectionString;
    private bool _prepared;

    public IdentityTestFactory()
    {
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
            });
        });

        // Swap the S3/MinIO adapters for in-memory fakes so artifact round-trips exercise the real
        // application + endpoint pipeline without requiring an external object store.
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IObjectStorage, InMemoryObjectStorage>();
            services.AddSingleton<IQuarantineStore, InMemoryQuarantineStore>();
        });
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