using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

public sealed class CoordinationEventConfiguration : IEntityTypeConfiguration<CoordinationEvent>
{
    public void Configure(EntityTypeBuilder<CoordinationEvent> builder)
    {
        builder.ToTable("coordination_events");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));

        builder.Property(e => e.ProjectId)
            .HasConversion(id => id.Value, value => new ProjectId(value));

        builder.Property(e => e.CampaignId)
            .HasConversion(id => id.Value, value => new CampaignId(value));

        builder.Property(e => e.ChannelId)
            .HasConversion(id => id != null ? id.Value : (Guid?)null, value => value != null ? new ChannelId(value.Value) : null);

        builder.Property(e => e.EventKey)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.EventType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.PayloadJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.Sequence);
        builder.Property(e => e.RecordedAt);

        builder.HasIndex(e => new { e.CampaignId, e.Sequence }).IsUnique();
    }
}
