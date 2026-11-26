using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

public sealed class WorkPackageDependencyConfiguration : IEntityTypeConfiguration<WorkPackageDependency>
{
    public void Configure(EntityTypeBuilder<WorkPackageDependency> builder)
    {
        builder.ToTable("work_package_dependencies");
        builder.HasKey(d => new { d.PackageId, d.DependsOnPackageId });

        builder.Property(d => d.PackageId)
            .HasConversion(id => id.Value, value => new WorkPackageId(value));
        builder.Property(d => d.DependsOnPackageId)
            .HasConversion(id => id.Value, value => new WorkPackageId(value));
        builder.Property(d => d.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));
        builder.Property(d => d.ProjectId)
            .HasConversion(id => id.Value, value => new ProjectId(value));
        builder.Property(d => d.CampaignId)
            .HasConversion(id => id.Value, value => new CampaignId(value));
        builder.Property(d => d.BaselineId);

        builder.HasIndex(d => new { d.TenantId, d.CampaignId, d.BaselineId });
    }
}
