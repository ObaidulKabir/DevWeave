using DevWeave.Domain.Commercial;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Coordination;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

public sealed class UsageObservationConfiguration : IEntityTypeConfiguration<UsageObservation>
{
    public void Configure(EntityTypeBuilder<UsageObservation> builder)
    {
        builder.ToTable("usage_observations");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .HasConversion(id => id.Value, value => new UsageObservationId(value));

        builder.Property(o => o.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));

        builder.Property(o => o.ProjectId)
            .HasConversion(id => id != null ? id.Value : (Guid?)null, value => value.HasValue ? new ProjectId(value.Value) : null);

        builder.Property(o => o.CampaignId)
            .HasConversion(id => id != null ? id.Value : (Guid?)null, value => value.HasValue ? new CampaignId(value.Value) : null);

        builder.Property(o => o.RuntimeId)
            .HasConversion(id => id != null ? id.Value : (Guid?)null, value => value.HasValue ? new RuntimeId(value.Value) : null);

        builder.Property(o => o.ExecutionMode).HasMaxLength(40).IsRequired();
        builder.Property(o => o.ResourceClass).HasMaxLength(40).IsRequired();
        builder.Property(o => o.Meter).HasMaxLength(60).IsRequired();
        builder.Property(o => o.Quantity).HasPrecision(18, 4);
        builder.Property(o => o.StartedAt);
        builder.Property(o => o.EndedAt);
        builder.Property(o => o.IdempotencyKey).HasMaxLength(255).IsRequired();

        builder.HasIndex(o => new { o.TenantId, o.IdempotencyKey }).IsUnique();
    }
}
