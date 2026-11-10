using DevWeave.Domain.Identity;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

internal sealed class MilestoneTransitionConfiguration : IEntityTypeConfiguration<MilestoneTransition>
{
    public void Configure(EntityTypeBuilder<MilestoneTransition> builder)
    {
        builder.ToTable("milestone_transitions");
        builder.HasKey(transition => transition.Id);
        builder.Property(transition => transition.Id)
            .HasConversion(id => id.Value, value => new MilestoneTransitionId(value))
            .ValueGeneratedNever();
        builder.Property(transition => transition.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();
        builder.Property(transition => transition.ProjectId)
            .HasConversion(id => id.Value, value => new ProjectId(value))
            .IsRequired();
        builder.Property(transition => transition.MilestoneId)
            .HasConversion(id => id.Value, value => new MilestoneId(value))
            .IsRequired();
        builder.Property(transition => transition.FromState).HasConversion<int>().IsRequired();
        builder.Property(transition => transition.ToState).HasConversion<int>().IsRequired();
        builder.Property(transition => transition.ActorId)
            .HasConversion(id => id.Value, value => new PrincipalId(value))
            .IsRequired();
        builder.Property(transition => transition.Reason).IsRequired().HasMaxLength(1000);
        builder.Property(transition => transition.Version).IsRequired();
        builder.Property(transition => transition.RecordedAt).IsRequired();
        builder.HasIndex(transition => new
            { transition.TenantId, transition.ProjectId, transition.MilestoneId, transition.Version })
            .IsUnique();
        builder.HasOne<Milestone>().WithMany()
            .HasForeignKey(transition => new
                { transition.MilestoneId, transition.ProjectId, transition.TenantId })
            .HasPrincipalKey(milestone => new
                { milestone.Id, milestone.ProjectId, milestone.TenantId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
