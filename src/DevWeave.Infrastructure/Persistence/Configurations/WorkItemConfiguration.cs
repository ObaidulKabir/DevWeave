using DevWeave.Domain.Identity;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

internal sealed class WorkItemConfiguration : IEntityTypeConfiguration<WorkItem>
{
    public void Configure(EntityTypeBuilder<WorkItem> builder)
    {
        builder.ToTable("work_items");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id)
            .HasConversion(id => id.Value, value => new WorkItemId(value))
            .ValueGeneratedNever();
        builder.Property(item => item.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();
        builder.Property(item => item.ProjectId)
            .HasConversion(id => id.Value, value => new ProjectId(value))
            .IsRequired();
        builder.Property(item => item.Key).IsRequired().HasMaxLength(100);
        builder.Property(item => item.Title).IsRequired().HasMaxLength(200);
        builder.Property(item => item.Description).IsRequired().HasMaxLength(4000);
        builder.Property(item => item.MilestoneId)
            .HasConversion(id => id == null ? (Guid?)null : id.Value,
                value => value == null ? null : new MilestoneId(value.Value));
        builder.Property(item => item.OwnerId)
            .HasConversion(id => id == null ? (Guid?)null : id.Value,
                value => value == null ? null : new PrincipalId(value.Value));
        builder.Property(item => item.DueAt);
        builder.Property(item => item.Priority).HasConversion<int>().IsRequired();
        builder.Property(item => item.PlannedState).HasConversion<int>().IsRequired();
        builder.Property(item => item.ReportedState).HasConversion<int>().IsRequired();
        builder.Property(item => item.VerificationState).HasConversion<int>().IsRequired();
        builder.Property(item => item.ApprovalState).HasConversion<int>().IsRequired();
        builder.Property(item => item.ReportedBy)
            .HasConversion(id => id == null ? (Guid?)null : id.Value,
                value => value == null ? null : new PrincipalId(value.Value));
        builder.Property(item => item.VerifiedBy)
            .HasConversion(id => id == null ? (Guid?)null : id.Value,
                value => value == null ? null : new PrincipalId(value.Value));
        builder.Property(item => item.ApprovedBy)
            .HasConversion(id => id == null ? (Guid?)null : id.Value,
                value => value == null ? null : new PrincipalId(value.Value));
        builder.Property(item => item.CreatedBy)
            .HasConversion(id => id.Value, value => new PrincipalId(value))
            .IsRequired();
        builder.Property(item => item.Version).IsConcurrencyToken().IsRequired();
        builder.Property(item => item.CreatedAt).IsRequired();
        builder.Property(item => item.UpdatedAt).IsRequired();

        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.Key }).IsUnique();
        builder.HasAlternateKey(item => new { item.Id, item.TenantId, item.ProjectId });
        builder.HasOne<Project>().WithMany()
            .HasForeignKey(item => new { item.ProjectId, item.TenantId })
            .HasPrincipalKey(project => new { project.Id, project.TenantId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Milestone>().WithMany()
            .HasForeignKey(item => new { item.MilestoneId, item.ProjectId, item.TenantId })
            .HasPrincipalKey(milestone => new { milestone.Id, milestone.ProjectId, milestone.TenantId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
