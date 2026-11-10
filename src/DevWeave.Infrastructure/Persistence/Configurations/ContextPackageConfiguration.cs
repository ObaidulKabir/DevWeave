using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

internal sealed class ContextPackageConfiguration : IEntityTypeConfiguration<ContextPackage>
{
    public void Configure(EntityTypeBuilder<ContextPackage> builder)
    {
        builder.ToTable("context_packages");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasConversion(id => id.Value, value => new ContextPackageId(value))
            .ValueGeneratedNever();

        builder.Property(p => p.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.Property(p => p.ProjectId)
            .HasConversion(id => id.Value, value => new ProjectId(value))
            .IsRequired();

        builder.Property(p => p.TaskKey)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.AgentId)
            .HasConversion(id => id.Value, value => new PrincipalId(value))
            .IsRequired();

        builder.Property(p => p.IdempotencyKey)
            .HasMaxLength(200);

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        // Revision-pinned entries are persisted as owned child rows (AGT-004, AGT-005).
        builder.OwnsMany(p => p.Entries, entries =>
        {
            entries.ToTable("context_package_entries");
            entries.WithOwner().HasForeignKey("context_package_id");

            entries.Property(e => e.ItemId)
                .HasColumnName("knowledge_item_id")
                .HasConversion(id => id.Value, value => new KnowledgeItemId(value));
            entries.Property(e => e.RevisionId)
                .HasColumnName("knowledge_revision_id")
                .HasConversion(id => id.Value, value => new KnowledgeRevisionId(value));
            entries.Property(e => e.Position)
                .HasColumnName("position")
                .IsRequired();
            entries.Property(e => e.Representation)
                .HasColumnName("representation_kind")
                .HasConversion<int>()
                .IsRequired();
        });

        builder.HasIndex(p => new { p.TenantId, p.ProjectId, p.IdempotencyKey });

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(p => p.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(p => p.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Principal>()
            .WithMany()
            .HasForeignKey(p => p.AgentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}