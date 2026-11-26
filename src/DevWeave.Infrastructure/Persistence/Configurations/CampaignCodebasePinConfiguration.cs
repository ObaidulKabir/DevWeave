using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

public sealed class CampaignCodebasePinConfiguration : IEntityTypeConfiguration<CampaignCodebasePin>
{
    public void Configure(EntityTypeBuilder<CampaignCodebasePin> builder)
    {
        builder.ToTable("campaign_codebase_pins");

        builder.HasKey(p => new { p.CampaignId, p.TargetId });

        builder.Property(p => p.CampaignId)
            .HasConversion(id => id.Value, value => new CampaignId(value));

        builder.Property(p => p.TargetId)
            .HasConversion(id => id.Value, value => new CodebaseTargetId(value));

        builder.Property(p => p.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));

        builder.Property(p => p.ProjectId)
            .HasConversion(id => id.Value, value => new ProjectId(value));

        builder.Property(p => p.Provider)
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(p => p.RepositoryId)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(p => p.TargetBranch)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(p => p.BaseCommitSha)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(p => p.PinnedAt);

        builder.HasIndex(p => new { p.TenantId, p.ProjectId, p.CampaignId });
    }
}
