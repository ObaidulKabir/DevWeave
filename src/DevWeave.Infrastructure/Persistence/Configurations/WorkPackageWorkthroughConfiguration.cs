using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

public sealed class WorkPackageWorkthroughConfiguration : IEntityTypeConfiguration<WorkPackageWorkthrough>
{
    public void Configure(EntityTypeBuilder<WorkPackageWorkthrough> builder)
    {
        builder.ToTable("work_package_workthroughs");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.Id)
            .HasConversion(id => id.Value, value => new WorkthroughId(value));

        builder.Property(w => w.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));

        builder.Property(w => w.ProjectId)
            .HasConversion(id => id.Value, value => new ProjectId(value));

        builder.Property(w => w.WorkPackageId)
            .HasConversion(id => id.Value, value => new WorkPackageId(value));

        builder.Property(w => w.RecordedBy)
            .HasConversion(id => id.Value, value => new PrincipalId(value));

        builder.Property(w => w.Kind)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(w => w.Outcome)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(w => w.WhatSucceeded)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(w => w.BlockedReason)
            .HasMaxLength(2000);

        builder.Property(w => w.Steps)
            .HasColumnType("text[]")
            .IsRequired();

        builder.Property(w => w.ChangeSetId);
        builder.Property(w => w.RecordedAt);

        builder.HasIndex(w => new { w.TenantId, w.WorkPackageId, w.RecordedAt });
    }
}
