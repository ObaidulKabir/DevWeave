using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

public sealed class PlanBaselineConfiguration : IEntityTypeConfiguration<PlanBaseline>
{
    public void Configure(EntityTypeBuilder<PlanBaseline> builder)
    {
        builder.ToTable("plan_baselines");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id)
            .HasConversion(id => id.Value, value => new BaselineId(value));

        builder.Property(b => b.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));

        builder.Property(b => b.ProjectId)
            .HasConversion(id => id.Value, value => new ProjectId(value));

        builder.Property(b => b.Goal)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(b => b.ApprovedRevisionIds)
            .HasColumnType("uuid[]");

        builder.Property(b => b.DependencyIds)
            .HasColumnType("uuid[]");

        builder.Property(b => b.AcceptanceCriteria)
            .HasColumnType("text[]");

        builder.Property(b => b.ExpectedPlanVersion);
        builder.Property(b => b.CreatedAt);

        // One baseline per plan version per project: two simultaneous locks cannot both win.
        builder.HasIndex(b => new { b.TenantId, b.ProjectId, b.ExpectedPlanVersion })
            .IsUnique();
    }
}
