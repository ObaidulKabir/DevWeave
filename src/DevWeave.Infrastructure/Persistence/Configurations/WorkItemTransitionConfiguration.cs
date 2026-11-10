using DevWeave.Domain.Identity;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

internal sealed class WorkItemTransitionConfiguration : IEntityTypeConfiguration<WorkItemTransition>
{
    public void Configure(EntityTypeBuilder<WorkItemTransition> builder)
    {
        builder.ToTable("work_item_transitions");
        builder.HasKey(transition => transition.Id);
        builder.Property(transition => transition.Id)
            .HasConversion(id => id.Value, value => new WorkItemTransitionId(value))
            .ValueGeneratedNever();
        builder.Property(transition => transition.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();
        builder.Property(transition => transition.ProjectId)
            .HasConversion(id => id.Value, value => new ProjectId(value))
            .IsRequired();
        builder.Property(transition => transition.WorkItemId)
            .HasConversion(id => id.Value, value => new WorkItemId(value))
            .IsRequired();
        builder.Property(transition => transition.Dimension).HasConversion<int>().IsRequired();
        builder.Property(transition => transition.FromState).IsRequired().HasMaxLength(40);
        builder.Property(transition => transition.ToState).IsRequired().HasMaxLength(40);
        builder.Property(transition => transition.ActorId)
            .HasConversion(id => id.Value, value => new PrincipalId(value))
            .IsRequired();
        builder.Property(transition => transition.Reason).IsRequired().HasMaxLength(1000);
        builder.Property(transition => transition.RecordedAt).IsRequired();
        builder.Property(transition => transition.Version).IsRequired();
        builder.HasIndex(transition => new
            { transition.TenantId, transition.ProjectId, transition.WorkItemId, transition.Version })
            .IsUnique();
        builder.HasOne<WorkItem>().WithMany()
            .HasForeignKey(transition => new
                { transition.WorkItemId, transition.TenantId, transition.ProjectId })
            .HasPrincipalKey(item => new { item.Id, item.TenantId, item.ProjectId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
