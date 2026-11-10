using DevWeave.Domain.Activities;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

internal sealed class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> builder)
    {
        builder.ToTable("activities");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasConversion(
                id => id.Value,
                value => new ActivityId(value))
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

        builder.Property(a => a.Type)
            .HasConversion<int>()
            .IsRequired();

        // Actor is a logical pointer to the acting principal (PRO-002), not a foreign key, matching
        // the provenance of KnowledgeItem.CreatedBy and Relationship.CreatedBy.
        builder.Property(a => a.Actor)
            .HasConversion(
                id => id.Value,
                value => new PrincipalId(value))
            .IsRequired();

        builder.Property(a => a.Purpose)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(a => a.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(a => a.StartedAt)
            .IsRequired();

        builder.Property(a => a.EndedAt);

        builder.Property(a => a.CorrelationId)
            .HasMaxLength(200);

        builder.Property(a => a.Reason)
            .HasMaxLength(1000);

        // A logical pointer to the referenced (superseded/corrected/retracted) revision (REV-008),
        // not a foreign key.
        builder.Property(a => a.Reference)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value == null ? null : new KnowledgeRevisionId(value.Value));

        // Revision-pinned inputs/outputs are persisted as owned child rows with a cascade delete to
        // the owning activity (SRS PRO-003, PRO-004).
        builder.OwnsMany(a => a.Inputs, inputs =>
        {
            inputs.ToTable("activity_inputs");
            inputs.WithOwner().HasForeignKey("activity_id");

            inputs.Property(i => i.ItemId)
                .HasColumnName("knowledge_item_id")
                .HasConversion(
                    id => id.Value,
                    value => new KnowledgeItemId(value));
            inputs.Property(i => i.RevisionId)
                .HasColumnName("knowledge_revision_id")
                .HasConversion(
                    id => id.Value,
                    value => new KnowledgeRevisionId(value));
            inputs.Property(i => i.Position)
                .HasColumnName("position")
                .IsRequired();
        });

        builder.OwnsMany(a => a.Outputs, outputs =>
        {
            outputs.ToTable("activity_outputs");
            outputs.WithOwner().HasForeignKey("activity_id");

            outputs.Property(o => o.ItemId)
                .HasColumnName("knowledge_item_id")
                .HasConversion(
                    id => id.Value,
                    value => new KnowledgeItemId(value));
            outputs.Property(o => o.RevisionId)
                .HasColumnName("knowledge_revision_id")
                .HasConversion(
                    id => id.Value,
                    value => new KnowledgeRevisionId(value));
            outputs.Property(o => o.Position)
                .HasColumnName("position")
                .IsRequired();
        });

        // Query-shaped indexes backing the repository's tenant-scoped reads.
        builder.HasIndex(a => new { a.TenantId, a.ProjectId });
        builder.HasIndex(a => new { a.TenantId, a.Actor });

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