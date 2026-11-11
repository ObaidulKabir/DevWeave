using DevWeave.Domain.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

internal sealed class InboundEventConfiguration : IEntityTypeConfiguration<InboundEvent>
{
    public void Configure(EntityTypeBuilder<InboundEvent> builder)
    {
        builder.ToTable("inbound_events");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasConversion(
                id => id.Value,
                value => new InboundEventId(value))
            .ValueGeneratedNever();

        builder.Property(e => e.IntegrationId)
            .HasConversion(
                id => id.Value,
                value => new IntegrationId(value))
            .IsRequired();

        builder.Property(e => e.ProviderEventId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.SentAt)
            .IsRequired();

        builder.Property(e => e.ReceivedAt)
            .IsRequired();

        builder.Property(e => e.MetadataJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.ProcessedAt);

        builder.HasIndex(e => new { e.IntegrationId, e.ProviderEventId })
            .IsUnique();

        builder.HasOne<Integration>()
            .WithMany()
            .HasForeignKey(e => e.IntegrationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
