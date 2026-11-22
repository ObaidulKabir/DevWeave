using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

internal sealed class PlanBaselineV2Configuration : IEntityTypeConfiguration<PlanBaselineV2>
{
    public void Configure(EntityTypeBuilder<PlanBaselineV2> builder)
    {
        builder.ToTable("plan_baseline_manifests");
        builder.HasKey(baseline => baseline.Id);

        builder.Property(baseline => baseline.Id)
            .HasConversion(id => id.Value, value => new BaselineId(value))
            .ValueGeneratedNever();
        builder.Property(baseline => baseline.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));
        builder.Property(baseline => baseline.ProjectId)
            .HasConversion(id => id.Value, value => new ProjectId(value));
        builder.Property(baseline => baseline.ManifestSchemaVersion)
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(baseline => baseline.HistoricalPrecision)
            .HasConversion<int>();
        builder.Property(baseline => baseline.LegacyAcceptanceCriteria)
            .HasColumnType("text[]");
        builder.Property(baseline => baseline.LifecycleProfileId);
        builder.Property(baseline => baseline.LifecycleProfileVersion)
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(baseline => baseline.PolicyVersion)
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(baseline => baseline.PredecessorBaselineId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value == null ? null : new BaselineId(value.Value));
        builder.Property(baseline => baseline.ManifestChecksum)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();
        builder.Property(baseline => baseline.CreatedAt);

        builder.OwnsMany(baseline => baseline.ApprovedRevisions, revisions =>
        {
            revisions.ToTable("plan_baseline_revision_refs");
            revisions.WithOwner().HasForeignKey("baseline_id");
            revisions.HasKey("baseline_id", nameof(BaselineRevisionRef.Position));
            revisions.Property(reference => reference.ItemId).HasColumnName("knowledge_item_id");
            revisions.Property(reference => reference.RevisionId).HasColumnName("knowledge_revision_id");
            revisions.Property(reference => reference.TypeId).HasMaxLength(200).HasColumnName("type_id");
            revisions.Property(reference => reference.SchemaVersion).HasMaxLength(100).HasColumnName("schema_version");
            revisions.Property(reference => reference.Position)
                .HasColumnName("position")
                .ValueGeneratedNever();
        });

        builder.OwnsMany(baseline => baseline.SemanticEdges, edges =>
        {
            edges.ToTable("plan_baseline_semantic_edges");
            edges.WithOwner().HasForeignKey("baseline_id");
            edges.HasKey("baseline_id", nameof(BaselineSemanticEdge.Position));
            edges.Property(edge => edge.RelationshipId).HasColumnName("relationship_id");
            edges.Property(edge => edge.SourceItemId).HasColumnName("source_item_id");
            edges.Property(edge => edge.TargetItemId).HasColumnName("target_item_id");
            edges.Property(edge => edge.RelationTypeCode).HasMaxLength(100).HasColumnName("relation_type_code");
            edges.Property(edge => edge.Position)
                .HasColumnName("position")
                .ValueGeneratedNever();
        });

        builder.OwnsMany(baseline => baseline.SpecificationBindings, bindings =>
        {
            bindings.ToTable("plan_baseline_specification_bindings");
            bindings.WithOwner().HasForeignKey("baseline_id");
            bindings.HasKey("baseline_id", nameof(BaselineSpecificationBinding.Position));
            bindings.Property(binding => binding.Topic).HasMaxLength(300).HasColumnName("topic");
            bindings.Property(binding => binding.ItemId).HasColumnName("knowledge_item_id");
            bindings.Property(binding => binding.RevisionId).HasColumnName("knowledge_revision_id");
            bindings.Property(binding => binding.BindingHash).HasMaxLength(64).HasColumnName("binding_hash");
            bindings.Property(binding => binding.Position)
                .HasColumnName("position")
                .ValueGeneratedNever();
        });

        builder.OwnsMany(baseline => baseline.CriterionRefs, criteria =>
        {
            criteria.ToTable("plan_baseline_criterion_refs");
            criteria.WithOwner().HasForeignKey("baseline_id");
            criteria.HasKey("baseline_id", nameof(BaselineCriterionRef.Position));
            criteria.Property(reference => reference.ItemId).HasColumnName("knowledge_item_id");
            criteria.Property(reference => reference.RevisionId).HasColumnName("knowledge_revision_id");
            criteria.Property(reference => reference.Position)
                .HasColumnName("position")
                .ValueGeneratedNever();
        });

        builder.OwnsMany(baseline => baseline.DependencyGraph, dependencies =>
        {
            dependencies.ToTable("plan_baseline_dependency_edges");
            dependencies.WithOwner().HasForeignKey("baseline_id");
            dependencies.HasKey("baseline_id", nameof(BaselineDependencyEdge.Position));
            dependencies.Property(edge => edge.FromKind)
                .HasConversion<int>()
                .HasColumnName("from_kind");
            dependencies.Property(edge => edge.FromIdentifier)
                .HasMaxLength(500)
                .HasColumnName("from_identifier");
            dependencies.Property(edge => edge.ToKind)
                .HasConversion<int>()
                .HasColumnName("to_kind");
            dependencies.Property(edge => edge.ToIdentifier)
                .HasMaxLength(500)
                .HasColumnName("to_identifier");
            dependencies.Property(edge => edge.RelationType)
                .HasMaxLength(100)
                .HasColumnName("relation_type");
            dependencies.Property(edge => edge.Position)
                .HasColumnName("position")
                .ValueGeneratedNever();
        });

        builder.HasIndex(baseline => new { baseline.TenantId, baseline.ProjectId });
        builder.HasIndex(baseline => baseline.ManifestChecksum);

        builder.HasOne<PlanBaseline>()
            .WithOne()
            .HasForeignKey<PlanBaselineV2>(baseline => baseline.Id)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(baseline => baseline.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(baseline => baseline.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
