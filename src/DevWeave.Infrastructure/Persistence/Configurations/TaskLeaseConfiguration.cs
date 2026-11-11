using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

public sealed class TaskLeaseConfiguration : IEntityTypeConfiguration<TaskLease>
{
    public void Configure(EntityTypeBuilder<TaskLease> builder)
    {
        builder.ToTable("task_leases");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .HasConversion(id => id.Value, value => new LeaseId(value));

        builder.Property(l => l.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));

        builder.Property(l => l.ProjectId)
            .HasConversion(id => id.Value, value => new ProjectId(value));

        builder.Property(l => l.WorkPackageId)
            .HasConversion(id => id.Value, value => new WorkPackageId(value));

        builder.Property(l => l.RuntimeId)
            .HasConversion(id => id.Value, value => new RuntimeId(value));

        builder.Property(l => l.ResourceScope)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(l => l.Status)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(l => l.Fence);
        builder.Property(l => l.ExpiresAt);
        builder.Property(l => l.CreatedAt);

        builder.HasIndex(l => new { l.WorkPackageId, l.ResourceScope }).IsUnique();
    }
}
