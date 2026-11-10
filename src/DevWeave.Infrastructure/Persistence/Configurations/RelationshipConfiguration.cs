using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Relations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

internal sealed class RelationshipConfiguration : IEntityTypeConfiguration<Relationship>
{
    public void Configure(EntityTypeBuilder<Relationship> builder)
    {
        builder.ToTable("relationships");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasConversion(
                id => id.Value,
                value => new RelationshipId(value))
            .ValueGeneratedNever();

        builder.Property(r => r.TenantId)
            .HasConversion(
                id => id.Value,
                value => new TenantId(value))
            .IsRequired();

        builder.Property(r => r.ProjectId)
            .HasConversion(
                id => id.Value,
                value => new ProjectId(value))
            .IsRequired();

        builder.Property(r => r.SourceItemId)
            .HasConversion(
                id => id.Value,
                value => new KnowledgeItemId(value))
            .IsRequired();

        builder.Property(r => r.TargetItemId)
            .HasConversion(
                id => id.Value,
                value => new KnowledgeItemId(value))
            .IsRequired();

        builder.Property(r => r.RelationTypeCode)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(r => r.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(r => r.CreatedAt)
            .IsRequired();

        // CreatedBy is a logical pointer to the acting principal, not a foreign key (mirrors KnowledgeItem).
        builder.Property(r => r.CreatedBy)
            .HasConversion(
                id => id.Value,
                value => new PrincipalId(value))
            .IsRequired();

        // Query-shaped indexes backing the repository's tenant-scoped reads.
        builder.HasIndex(r => new { r.TenantId, r.ProjectId });
        builder.HasIndex(r => new { r.TenantId, r.SourceItemId });
        builder.HasIndex(r => new { r.TenantId, r.TargetItemId });

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(r => r.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(r => r.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<KnowledgeItem>()
            .WithMany()
            .HasForeignKey(r => r.SourceItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<KnowledgeItem>()
            .WithMany()
            .HasForeignKey(r => r.TargetItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}