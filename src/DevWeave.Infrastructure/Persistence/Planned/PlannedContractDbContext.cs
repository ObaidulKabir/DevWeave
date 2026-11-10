using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DevWeave.Infrastructure.Persistence.Planned;

/// <summary>
/// Isolated EF design-time schema only. Not registered in DI, not migrated by the live API,
/// and not an implementation of any application port.
/// </summary>
public sealed class PlannedContractDbContext(DbContextOptions<PlannedContractDbContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (var table in PlannedSchemaCatalog.Tables)
        {
            modelBuilder.SharedTypeEntity<Dictionary<string, object>>(table.Name, entity =>
            {
                entity.ToTable(table.Name);
                entity.IndexerProperty<Guid>("Id").ValueGeneratedNever();
                entity.HasKey("Id");
                if (table.TenantScoped)
                {
                    entity.IndexerProperty<Guid>("TenantId").IsRequired();
                    entity.HasIndex("Id", "TenantId").IsUnique();
                    entity.HasIndex("TenantId");
                }
                if (table.ProjectScoped)
                {
                    entity.IndexerProperty<Guid>("ProjectId").IsRequired();
                    entity.HasIndex("TenantId", "ProjectId");
                }

                entity.IndexerProperty<DateTimeOffset>("CreatedAt").IsRequired();
                foreach (var column in table.Columns)
                {
                    var property = entity.IndexerProperty(column.ClrType, column.Name)
                        .IsRequired(column.Required);
                    if (column.MaxLength is int maxLength)
                        property.HasMaxLength(maxLength);
                }
                if (table.UniqueScope is { Length: > 0 })
                    entity.HasIndex(table.UniqueScope).IsUnique();
            });
        }
    }
}

/// <summary>
/// Design-time-only factory, deliberately disconnected from the production connection string.
/// Generating a migration does not apply it or modify the registered DevWeaveDbContext.
/// </summary>
public sealed class PlannedContractDesignTimeFactory : IDesignTimeDbContextFactory<PlannedContractDbContext>
{
    public PlannedContractDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PlannedContractDbContext>()
            .UseNpgsql("Host=localhost;Database=devweave_contract_design",
                npgsql => npgsql.MigrationsHistoryTable("__EFPlannedContractHistory"))
            .Options;
        return new PlannedContractDbContext(options);
    }
}
