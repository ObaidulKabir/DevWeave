using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

public sealed class CoordinationChannelConfiguration : IEntityTypeConfiguration<CoordinationChannel>
{
    public void Configure(EntityTypeBuilder<CoordinationChannel> builder)
    {
        builder.ToTable("coordination_channels");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasConversion(id => id.Value, value => new ChannelId(value));

        builder.Property(c => c.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));

        builder.Property(c => c.ProjectId)
            .HasConversion(id => id.Value, value => new ProjectId(value));

        builder.Property(c => c.CampaignId)
            .HasConversion(id => id != null ? id.Value : (Guid?)null, value => value != null ? new CampaignId(value.Value) : null);

        builder.Property(c => c.WorkPackageId)
            .HasConversion(id => id != null ? id.Value : (Guid?)null, value => value != null ? new WorkPackageId(value.Value) : null);

        builder.Property(c => c.ChannelType)
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(c => c.Visibility)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(c => c.SequenceHighWatermark);
        builder.Property(c => c.CreatedAt);
    }
}
