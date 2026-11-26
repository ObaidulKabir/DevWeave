using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

public sealed class WorkPackageAssignmentConfiguration : IEntityTypeConfiguration<WorkPackageAssignment>
{
    public void Configure(EntityTypeBuilder<WorkPackageAssignment> builder)
    {
        builder.ToTable("work_package_assignments");
        builder.HasKey(a => a.PackageId);

        builder.Property(a => a.PackageId)
            .HasConversion(id => id.Value, value => new WorkPackageId(value));
        builder.Property(a => a.RuntimeId)
            .HasConversion(id => id.Value, value => new RuntimeId(value));
        builder.Property(a => a.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));
        builder.Property(a => a.ProjectId)
            .HasConversion(id => id.Value, value => new ProjectId(value));
        builder.Property(a => a.CampaignId)
            .HasConversion(id => id.Value, value => new CampaignId(value));
        builder.Property(a => a.BaselineId);
        builder.Property(a => a.ContextSnapshotId);
        builder.Property(a => a.SnapshotRevisionIds).HasColumnType("uuid[]").IsRequired();
        builder.Property(a => a.AssignedAt);

        builder.HasIndex(a => new { a.TenantId, a.CampaignId, a.RuntimeId });
    }
}
