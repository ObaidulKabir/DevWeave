using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

public sealed class ParticipationOfferConfiguration : IEntityTypeConfiguration<ParticipationOffer>
{
    public void Configure(EntityTypeBuilder<ParticipationOffer> builder)
    {
        builder.ToTable("participation_offers");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .HasConversion(id => id.Value, value => new ParticipationOfferId(value));

        builder.Property(o => o.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));

        builder.Property(o => o.ProjectId)
            .HasConversion(id => id.Value, value => new ProjectId(value));

        builder.Property(o => o.CampaignId)
            .HasConversion(id => id.Value, value => new CampaignId(value));

        builder.Property(o => o.RuntimeId)
            .HasConversion(id => id.Value, value => new RuntimeId(value));

        builder.Property(o => o.RequiredCapabilities)
            .HasColumnType("text[]");

        builder.Property(o => o.AvailableUntil);
        builder.Property(o => o.Status).HasConversion<string>();
        builder.Property(o => o.Reason).HasMaxLength(500);
        builder.Property(o => o.CreatedAt);
        builder.Property(o => o.DecidedAt);
    }
}
