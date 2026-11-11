using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

public sealed class IntegrationQueueItemConfiguration : IEntityTypeConfiguration<IntegrationQueueItem>
{
    public void Configure(EntityTypeBuilder<IntegrationQueueItem> builder)
    {
        builder.ToTable("integration_queue_items");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasConversion(id => id.Value, value => new IntegrationQueueItemId(value));

        builder.Property(i => i.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));

        builder.Property(i => i.ProjectId)
            .HasConversion(id => id.Value, value => new ProjectId(value));

        builder.Property(i => i.ChangeSetId)
            .HasConversion(id => id.Value, value => new ChangeSetId(value));

        builder.Property(i => i.IdempotencyKey)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(i => i.Status).HasConversion<string>();
        builder.Property(i => i.Outcome).HasMaxLength(1000);
        builder.Property(i => i.Fence);
        builder.Property(i => i.CreatedAt);
        builder.Property(i => i.DecidedAt);

        builder.HasIndex(i => new { i.TenantId, i.IdempotencyKey }).IsUnique();
    }
}
