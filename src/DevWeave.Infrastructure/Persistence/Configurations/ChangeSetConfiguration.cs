using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

public sealed class ChangeSetConfiguration : IEntityTypeConfiguration<ChangeSet>
{
    public void Configure(EntityTypeBuilder<ChangeSet> builder)
    {
        builder.ToTable("change_sets");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasConversion(id => id.Value, value => new ChangeSetId(value));

        builder.Property(c => c.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));

        builder.Property(c => c.ProjectId)
            .HasConversion(id => id.Value, value => new ProjectId(value));

        builder.Property(c => c.WorkPackageId)
            .HasConversion(id => id.Value, value => new WorkPackageId(value));

        builder.Property(c => c.CodebaseTargetId)
            .HasConversion(id => id.Value, value => new CodebaseTargetId(value));

        builder.Property(c => c.ProviderRef)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(c => c.CommitSha)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(c => c.ChangedPathMetadata)
            .HasColumnType("text[]");

        builder.Property(c => c.RecordedAt);
    }
}
