using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

internal sealed class KnowledgeItemConfiguration : IEntityTypeConfiguration<KnowledgeItem>
{
    public void Configure(EntityTypeBuilder<KnowledgeItem> builder)
    {
        builder.ToTable("knowledge_items");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasConversion(
                id => id.Value,
                value => new KnowledgeItemId(value))
            .ValueGeneratedNever();

        builder.Property(i => i.TenantId)
            .HasConversion(
                id => id.Value,
                value => new TenantId(value))
            .IsRequired();

        builder.Property(i => i.ProjectId)
            .HasConversion(
                id => id.Value,
                value => new ProjectId(value))
            .IsRequired();

        builder.Property(i => i.Key)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(i => i.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(i => i.Classification)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(i => i.RepresentationKind)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(i => i.State)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(i => i.ReviewState)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(i => i.VerificationState)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(i => i.CurrencyState)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(i => i.TypeId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(i => i.SchemaVersion)
            .IsRequired()
            .HasMaxLength(50);

        // A logical pointer (not a foreign key) to the current revision, advanced atomically on each
        // new revision (BR-006). Kept as a nullable scalar value object.
        builder.Property(i => i.CurrentRevisionId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value == null ? null : new KnowledgeRevisionId(value.Value));

        builder.Property(i => i.CurrentRevisionNumber)
            .IsRequired();

        builder.Property(i => i.CreatedBy)
            .HasConversion(
                id => id.Value,
                value => new PrincipalId(value))
            .IsRequired();

        builder.Property(i => i.CreatedAt)
            .IsRequired();

        // Key is unique within its project (SRS KNW-001 / invariant #2).
        builder.HasIndex(i => new { i.TenantId, i.ProjectId, i.Key })
            .IsUnique();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(i => i.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(i => i.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}