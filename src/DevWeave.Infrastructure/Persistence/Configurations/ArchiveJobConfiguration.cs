using DevWeave.Domain.Archives;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

internal sealed class ArchiveJobConfiguration : IEntityTypeConfiguration<ArchiveJob>
{
    public void Configure(EntityTypeBuilder<ArchiveJob> builder)
    {
        builder.ToTable("archive_jobs");
        builder.HasKey(job => job.Id);
        builder.Property(job => job.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();
        builder.Property(job => job.ProjectId)
            .HasConversion(id => id.Value, value => new ProjectId(value))
            .IsRequired();
        builder.Property(job => job.Kind).IsRequired().HasMaxLength(40);
        builder.Property(job => job.IdempotencyKey).IsRequired().HasMaxLength(200);
        builder.Property(job => job.ObjectKey).IsRequired().HasMaxLength(500);
        builder.Property(job => job.Checksum).IsRequired().HasMaxLength(128);
        builder.Property(job => job.FormatVersion).IsRequired().HasMaxLength(80);
        builder.Property(job => job.IdentityMapJson).HasColumnType("jsonb");
        builder.Property(job => job.CreatedAt).IsRequired();
        builder.HasIndex(job => new { job.TenantId, job.ProjectId, job.Kind, job.IdempotencyKey }).IsUnique();
    }
}
