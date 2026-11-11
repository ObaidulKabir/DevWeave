using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

public sealed class WorkPackageConfiguration : IEntityTypeConfiguration<WorkPackage>
{
    public void Configure(EntityTypeBuilder<WorkPackage> builder)
    {
        builder.ToTable("work_packages");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.Id)
            .HasConversion(id => id.Value, value => new WorkPackageId(value));

        builder.Property(w => w.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));

        builder.Property(w => w.ProjectId)
            .HasConversion(id => id.Value, value => new ProjectId(value));

        builder.Property(w => w.CampaignId)
            .HasConversion(id => id.Value, value => new CampaignId(value));

        builder.Property(w => w.Key)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(w => w.Objective)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(w => w.Status)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(w => w.BaselineId);
        builder.Property(w => w.Version);
        builder.Property(w => w.CreatedAt);
        builder.Property(w => w.UpdatedAt);

        builder.HasIndex(w => new { w.CampaignId, w.Key }).IsUnique();
    }
}
