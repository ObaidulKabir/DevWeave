using DevWeave.Domain.Commercial;
using DevWeave.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

public sealed class EntitlementDecisionConfiguration : IEntityTypeConfiguration<EntitlementDecision>
{
    public void Configure(EntityTypeBuilder<EntitlementDecision> builder)
    {
        builder.ToTable("entitlement_decisions");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasConversion(id => id.Value, value => new EntitlementDecisionId(value));
        builder.Property(d => d.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));
        builder.Property(d => d.ActorId)
            .HasConversion(id => id.Value, value => new PrincipalId(value));
        builder.Property(d => d.PlanId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value == null ? null : new CommercialPlanId(value.Value));
        builder.Property(d => d.PlanVersion).HasMaxLength(20);
        builder.Property(d => d.Meter).HasMaxLength(60).IsRequired();
        builder.Property(d => d.Requested).HasPrecision(18, 4);
        builder.Property(d => d.Observed).HasPrecision(18, 4);
        builder.Property(d => d.Outcome).HasMaxLength(40).IsRequired();
        builder.Property(d => d.Reason).HasMaxLength(500).IsRequired();
        builder.Property(d => d.PolicyVersion).HasMaxLength(20).IsRequired();
        builder.Property(d => d.DecidedAt).IsRequired();

        builder.HasIndex(d => new { d.TenantId, d.DecidedAt });
    }
}
