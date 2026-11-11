using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

public sealed class CodebaseTargetConfiguration : IEntityTypeConfiguration<CodebaseTarget>
{
    public void Configure(EntityTypeBuilder<CodebaseTarget> builder)
    {
        builder.ToTable("codebase_targets");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasConversion(id => id.Value, value => new CodebaseTargetId(value));

        builder.Property(t => t.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));

        builder.Property(t => t.ProjectId)
            .HasConversion(id => id.Value, value => new ProjectId(value));

        builder.Property(t => t.Provider)
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(t => t.RepositoryId)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(t => t.TargetBranch)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(t => t.BaseCommitSha)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(t => t.CreatedAt);
    }
}
