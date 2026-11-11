using DevWeave.Domain.Audit;
using DevWeave.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

public sealed class AuditRecordConfiguration : IEntityTypeConfiguration<AuditRecord>
{
    public void Configure(EntityTypeBuilder<AuditRecord> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasConversion(id => id.Value, value => new AuditRecordId(value));

        builder.Property(a => a.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));

        builder.Property(a => a.ActorId)
            .HasConversion(id => id.Value, value => new PrincipalId(value));

        builder.Property(a => a.Action)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.ResourceType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.ResourceId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.MetadataJson)
            .HasColumnType("jsonb");

        builder.Property(a => a.CorrelationId)
            .HasMaxLength(100);

        builder.HasIndex(a => new { a.TenantId, a.OccurredAt });
    }
}
