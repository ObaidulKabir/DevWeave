using DevWeave.Domain.Commercial;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

public sealed class EntitlementRuleConfiguration : IEntityTypeConfiguration<EntitlementRule>
{
    public void Configure(EntityTypeBuilder<EntitlementRule> builder)
    {
        builder.ToTable("entitlement_rules");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasConversion(id => id.Value, value => new EntitlementRuleId(value));

        builder.Property(r => r.PlanId)
            .HasConversion(id => id.Value, value => new CommercialPlanId(value));

        builder.Property(r => r.Key).HasMaxLength(60).IsRequired();
        builder.Property(r => r.Limit).HasPrecision(18, 4);
        builder.Property(r => r.Unit).HasMaxLength(20).IsRequired();
        builder.Property(r => r.OverageRate).HasPrecision(18, 4);
        builder.Property(r => r.Action).HasConversion<string>();

        builder.HasIndex(r => new { r.PlanId, r.Key }).IsUnique();
    }
}
