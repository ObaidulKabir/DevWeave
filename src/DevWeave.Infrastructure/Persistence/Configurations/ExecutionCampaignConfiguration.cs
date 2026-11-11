using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

public sealed class ExecutionCampaignConfiguration : IEntityTypeConfiguration<ExecutionCampaign>
{
    public void Configure(EntityTypeBuilder<ExecutionCampaign> builder)
    {
        builder.ToTable("execution_campaigns");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasConversion(id => id.Value, value => new CampaignId(value));

        builder.Property(c => c.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));

        builder.Property(c => c.ProjectId)
            .HasConversion(id => id.Value, value => new ProjectId(value));

        builder.Property(c => c.Objective)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(c => c.ExecutionMode)
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(c => c.Status)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(c => c.BaselineId);
        builder.Property(c => c.Version);
        builder.Property(c => c.CreatedAt);
        builder.Property(c => c.UpdatedAt);
    }
}
