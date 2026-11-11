using DevWeave.Domain.Common;
using DevWeave.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.TenantId)
            .HasConversion(
                id => id.Value,
                value => new TenantId(value))
            .IsRequired();

        builder.Property(m => m.ActorId)
            .HasConversion(
                id => id.Value,
                value => new PrincipalId(value))
            .IsRequired();

        builder.Property(m => m.EventKey)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(m => m.EventType)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(m => m.SchemaVersion)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(m => m.PayloadJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(m => m.CorrelationId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(m => m.OccurredAt)
            .IsRequired();

        builder.Property(m => m.ProcessedAt);

        builder.HasIndex(m => new { m.TenantId, m.EventKey })
            .IsUnique();

        builder.HasIndex(m => m.ProcessedAt)
            .HasFilter("\"ProcessedAt\" IS NULL");

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(m => m.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
