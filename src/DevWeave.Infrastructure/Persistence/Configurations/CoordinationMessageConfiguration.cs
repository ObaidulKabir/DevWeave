using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

public sealed class CoordinationMessageConfiguration : IEntityTypeConfiguration<CoordinationMessage>
{
    public void Configure(EntityTypeBuilder<CoordinationMessage> builder)
    {
        builder.ToTable("coordination_messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasConversion(id => id.Value, value => new MessageId(value));

        builder.Property(m => m.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));

        builder.Property(m => m.ProjectId)
            .HasConversion(id => id.Value, value => new ProjectId(value));

        builder.Property(m => m.ChannelId)
            .HasConversion(id => id.Value, value => new ChannelId(value));

        builder.Property(m => m.SenderId)
            .HasConversion(id => id.Value, value => new PrincipalId(value));

        builder.Property(m => m.RuntimeId)
            .HasConversion(id => id.Value, value => new RuntimeId(value));

        builder.Property(m => m.ParentMessageId)
            .HasConversion(id => id != null ? id.Value : (Guid?)null, value => value != null ? new MessageId(value.Value) : null);

        builder.Property(m => m.MessageType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(m => m.SchemaVersion)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(m => m.PayloadJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(m => m.IdempotencyKey)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(m => m.Sequence);
        builder.Property(m => m.CreatedAt);

        builder.HasIndex(m => new { m.ChannelId, m.Sequence }).IsUnique();
        builder.HasIndex(m => new { m.TenantId, m.IdempotencyKey }).IsUnique();
    }
}
