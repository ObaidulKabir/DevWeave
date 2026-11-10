using DevWeave.Domain.Artifacts;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

internal sealed class ArtifactConfiguration : IEntityTypeConfiguration<Artifact>
{
    public void Configure(EntityTypeBuilder<Artifact> builder)
    {
        builder.ToTable("artifacts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasConversion(
                id => id.Value,
                value => new ArtifactId(value))
            .ValueGeneratedNever();

        builder.Property(a => a.TenantId)
            .HasConversion(
                id => id.Value,
                value => new TenantId(value))
            .IsRequired();

        builder.Property(a => a.ProjectId)
            .HasConversion(
                id => id.Value,
                value => new ProjectId(value))
            .IsRequired();

        builder.Property(a => a.Key)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.Version)
            .IsRequired();

        builder.Property(a => a.MediaType)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.SizeInBytes)
            .IsRequired();

        builder.Property(a => a.Checksum)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(a => a.ChecksumAlgorithm)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(a => a.ObjectKey)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(a => a.Classification)
            .HasConversion<int>()
            .IsRequired();

        // A logical pointer to the creating principal (ART-002), not a foreign key.
        builder.Property(a => a.Creator)
            .HasConversion(
                id => id.Value,
                value => new PrincipalId(value))
            .IsRequired();

        builder.Property(a => a.CreatedAt)
            .IsRequired();

        builder.Property(a => a.Status)
            .HasConversion<int>()
            .IsRequired();

        // Query-shaped indexes backing the repository's tenant-scoped reads.
        builder.HasIndex(a => new { a.TenantId, a.ProjectId });

        // ART-003 immutability: a logical key has at most one row per version; a replacement is a new row.
        builder.HasIndex(a => new { a.TenantId, a.ProjectId, a.Key, a.Version })
            .IsUnique();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(a => a.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(a => a.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}