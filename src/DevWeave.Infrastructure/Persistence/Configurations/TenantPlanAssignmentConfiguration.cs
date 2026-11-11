using DevWeave.Domain.Commercial;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

public sealed class TenantPlanAssignmentConfiguration : IEntityTypeConfiguration<TenantPlanAssignment>
{
    public void Configure(EntityTypeBuilder<TenantPlanAssignment> builder)
    {
        builder.ToTable("tenant_plan_assignments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasConversion(id => id.Value, value => new TenantPlanAssignmentId(value));

        builder.Property(a => a.TenantId)
            .HasConversion(id => id.Value, value => new Domain.Identity.TenantId(value));

        builder.Property(a => a.PlanId)
            .HasConversion(id => id.Value, value => new CommercialPlanId(value));

        builder.Property(a => a.EffectiveFrom);
        builder.Property(a => a.EffectiveTo);
        builder.Property(a => a.IsActive);

        builder.HasIndex(a => new { a.TenantId, a.IsActive });
    }
}
