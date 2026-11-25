using DevWeave.Domain.Commercial;
using DevWeave.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

public sealed class TenantTrainingPolicyConfiguration : IEntityTypeConfiguration<TenantTrainingPolicy>
{
    public void Configure(EntityTypeBuilder<TenantTrainingPolicy> builder)
    {
        builder.ToTable("tenant_training_policies");
        builder.HasKey(p => p.TenantId);
        builder.Property(p => p.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));
        builder.Property(p => p.OptedIn).IsRequired();
        builder.Property(p => p.ActorId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value == null ? null : new PrincipalId(value.Value));
        builder.Property(p => p.ChangedAt);
        builder.Property(p => p.Reason).HasMaxLength(500);
    }
}
