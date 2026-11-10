using DevWeave.Domain.Identity;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

internal sealed class MilestoneConfiguration : IEntityTypeConfiguration<Milestone>
{
    public void Configure(EntityTypeBuilder<Milestone> builder)
    {
        builder.ToTable("milestones");
        builder.HasKey(milestone => milestone.Id);
        builder.Property(milestone => milestone.Id)
            .HasConversion(id => id.Value, value => new MilestoneId(value))
            .ValueGeneratedNever();
        builder.Property(milestone => milestone.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();
        builder.Property(milestone => milestone.ProjectId)
            .HasConversion(id => id.Value, value => new ProjectId(value))
            .IsRequired();
        builder.Property(milestone => milestone.Key).IsRequired().HasMaxLength(100);
        builder.Property(milestone => milestone.Title).IsRequired().HasMaxLength(200);
        builder.Property(milestone => milestone.DueAt);
        builder.Property(milestone => milestone.State).HasConversion<int>().IsRequired();
        builder.Property(milestone => milestone.CreatedBy)
            .HasConversion(id => id.Value, value => new PrincipalId(value))
            .IsRequired();
        builder.Property(milestone => milestone.CreatedAt).IsRequired();
        builder.Property(milestone => milestone.Version).IsConcurrencyToken().IsRequired();
        builder.Property(milestone => milestone.UpdatedAt).IsRequired();
        builder.HasIndex(milestone => new { milestone.TenantId, milestone.ProjectId, milestone.Key })
            .IsUnique();
        builder.HasAlternateKey(milestone => new { milestone.Id, milestone.ProjectId, milestone.TenantId });
        builder.HasOne<Project>().WithMany()
            .HasForeignKey(milestone => new { milestone.ProjectId, milestone.TenantId })
            .HasPrincipalKey(project => new { project.Id, project.TenantId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
