using DevWeave.Domain.Identity;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

internal sealed class WorkItemDependencyConfiguration : IEntityTypeConfiguration<WorkItemDependency>
{
    public void Configure(EntityTypeBuilder<WorkItemDependency> builder)
    {
        builder.ToTable("work_item_dependencies");
        builder.HasKey(dependency => dependency.Id);
        builder.Property(dependency => dependency.Id)
            .HasConversion(id => id.Value, value => new WorkItemDependencyId(value))
            .ValueGeneratedNever();
        builder.Property(dependency => dependency.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();
        builder.Property(dependency => dependency.ProjectId)
            .HasConversion(id => id.Value, value => new ProjectId(value))
            .IsRequired();
        builder.Property(dependency => dependency.WorkItemId)
            .HasConversion(id => id.Value, value => new WorkItemId(value))
            .IsRequired();
        builder.Property(dependency => dependency.DependsOnId)
            .HasConversion(id => id.Value, value => new WorkItemId(value))
            .IsRequired();
        builder.Property(dependency => dependency.CreatedBy)
            .HasConversion(id => id.Value, value => new PrincipalId(value))
            .IsRequired();
        builder.Property(dependency => dependency.CreatedAt).IsRequired();
        builder.HasIndex(dependency => new
            { dependency.TenantId, dependency.ProjectId, dependency.WorkItemId, dependency.DependsOnId })
            .IsUnique();
        builder.HasOne<WorkItem>().WithMany()
            .HasForeignKey(dependency => new
                { dependency.WorkItemId, dependency.TenantId, dependency.ProjectId })
            .HasPrincipalKey(item => new { item.Id, item.TenantId, item.ProjectId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkItem>().WithMany()
            .HasForeignKey(dependency => new
                { dependency.DependsOnId, dependency.TenantId, dependency.ProjectId })
            .HasPrincipalKey(item => new { item.Id, item.TenantId, item.ProjectId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
