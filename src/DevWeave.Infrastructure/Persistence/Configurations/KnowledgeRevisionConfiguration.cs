using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

internal sealed class KnowledgeRevisionConfiguration : IEntityTypeConfiguration<KnowledgeRevision>
{
    public void Configure(EntityTypeBuilder<KnowledgeRevision> builder)
    {
        builder.ToTable("knowledge_revisions");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasConversion(
                id => id.Value,
                value => new KnowledgeRevisionId(value))
            .ValueGeneratedNever();

        builder.Property(r => r.TenantId)
            .HasConversion(
                id => id.Value,
                value => new TenantId(value))
            .IsRequired();

        builder.Property(r => r.KnowledgeItemId)
            .HasConversion(
                id => id.Value,
                value => new KnowledgeItemId(value))
            .IsRequired();

        builder.Property(r => r.RevisionNumber)
            .IsRequired();

        builder.Property(r => r.SchemaVersion)
            .IsRequired()
            .HasMaxLength(50);

        // The raw representation payload. Schema validation is a later slice; the column is typed
        // jsonb so the raw JSON is retained verbatim (SRS KNW-002/knowledge representation).
        builder.Property(r => r.Content)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(r => r.Actor)
            .HasConversion(
                id => id.Value,
                value => new PrincipalId(value))
            .IsRequired();

        builder.Property(r => r.RecordedAt)
            .IsRequired();

        builder.Property(r => r.Reason)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(r => r.SourceReference)
            .HasMaxLength(2000);

        // A logical pointer to a superseded revision used for corrections (REV-008), not a foreign key.
        builder.Property(r => r.SupersededRevisionId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value == null ? null : new KnowledgeRevisionId(value.Value));

        builder.HasIndex(r => new { r.TenantId, r.KnowledgeItemId, r.RevisionNumber })
            .IsUnique();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(r => r.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<KnowledgeItem>()
            .WithMany()
            .HasForeignKey(r => r.KnowledgeItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}