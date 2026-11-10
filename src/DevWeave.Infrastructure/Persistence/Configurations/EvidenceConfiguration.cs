using DevWeave.Domain.Activities;
using DevWeave.Domain.Artifacts;
using DevWeave.Domain.Evidence;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

internal sealed class EvidenceConfiguration : IEntityTypeConfiguration<Evidence>
{
    public void Configure(EntityTypeBuilder<Evidence> builder)
    {
        builder.ToTable("evidence");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasConversion(
                id => id.Value,
                value => new EvidenceId(value))
            .ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .HasConversion(
                id => id.Value,
                value => new TenantId(value))
            .IsRequired();

        builder.Property(e => e.ProjectId)
            .HasConversion(
                id => id.Value,
                value => new ProjectId(value))
            .IsRequired();

        // A logical pointer to the originating claim activity (PRO-006), not a foreign key.
        builder.Property(e => e.OriginActivityId)
            .HasConversion(
                id => id.Value,
                value => new ActivityId(value))
            .IsRequired();

        builder.Property(e => e.RecordedAt)
            .IsRequired();

        builder.Property(e => e.Type)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.IntegrityHash)
            .HasMaxLength(200);

        // A logical pointer to the related artifact (PRO-006), not a foreign key.
        builder.Property(e => e.RelatedArtifactId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value == null ? null : new ArtifactId(value.Value));

        builder.Property(e => e.RelatedCriterion)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.ReviewState)
            .HasConversion<int>()
            .IsRequired();

        // A logical pointer to the reviewing principal (PRO-006), not a foreign key.
        builder.Property(e => e.ReviewedBy)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value == null ? null : new PrincipalId(value.Value));

        builder.Property(e => e.ReviewedAt);

        // Query-shaped indexes backing the repository's tenant-scoped reads.
        builder.HasIndex(e => new { e.TenantId, e.ProjectId });
        builder.HasIndex(e => new { e.TenantId, e.OriginActivityId });

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}