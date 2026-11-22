using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevWeave.Infrastructure.Persistence.Configurations;

internal sealed class KnowledgeTypeDefinitionConfiguration : IEntityTypeConfiguration<KnowledgeTypeDefinition>
{
    private static readonly DateTimeOffset SeededAt = new(2026, 11, 20, 0, 0, 0, TimeSpan.Zero);

    public void Configure(EntityTypeBuilder<KnowledgeTypeDefinition> builder)
    {
        builder.ToTable("knowledge_type_definitions");
        builder.HasKey(type => type.TypeId);

        builder.Property(type => type.TypeId)
            .HasMaxLength(200)
            .ValueGeneratedNever();
        builder.Property(type => type.Owner)
            .IsRequired()
            .HasMaxLength(200);
        builder.Property(type => type.Description)
            .IsRequired()
            .HasMaxLength(1000);
        builder.Property(type => type.StorageKind)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(type => type.LifecyclePolicy)
            .IsRequired()
            .HasMaxLength(200);
        builder.Property(type => type.DefaultClassification)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(type => type.IsCanonical)
            .IsRequired();
        builder.Property(type => type.MigrationStrategy)
            .IsRequired()
            .HasMaxLength(500);
        builder.Property(type => type.CreatedAt)
            .IsRequired();

        builder.HasIndex(type => type.IsCanonical);
        SeedCanonicalTypes(builder);
    }

    private static void SeedCanonicalTypes(EntityTypeBuilder<KnowledgeTypeDefinition> builder)
    {
        builder.HasData(
            Seed("strategy.goal", "Business/product objective.", KnowledgeStorageKind.KnowledgeEnvelope, Classification.Internal),
            Seed("strategy.assumption", "Assumption requiring validation.", KnowledgeStorageKind.KnowledgeEnvelope, Classification.Internal),
            Seed("requirements.functional", "Observable functional behavior.", KnowledgeStorageKind.KnowledgeEnvelope, Classification.Internal),
            Seed("requirements.nonfunctional", "Measurable quality or operational requirement.", KnowledgeStorageKind.KnowledgeEnvelope, Classification.Internal),
            Seed("requirements.constraint", "Design, legal, technical, or business constraint.", KnowledgeStorageKind.KnowledgeEnvelope, Classification.Internal),
            Seed("architecture.decision", "Architecture decision and rationale.", KnowledgeStorageKind.KnowledgeEnvelope, Classification.Internal),
            Seed("architecture.component", "Logical or deployable component.", KnowledgeStorageKind.KnowledgeEnvelope, Classification.Internal),
            Seed("architecture.interface", "Contract between components or systems.", KnowledgeStorageKind.KnowledgeEnvelope, Classification.Internal),
            Seed("specification.technical", "Detailed technical specification.", KnowledgeStorageKind.KnowledgeEnvelope, Classification.Internal),
            Seed("planning.work-item", "Planned unit of work.", KnowledgeStorageKind.WorkItemAggregate, Classification.Internal, aggregate: true),
            Seed("execution.activity-report", "Human or agent work summary.", KnowledgeStorageKind.ActivityAggregate, Classification.Internal, aggregate: true),
            Seed("verification.result", "Test or assessment result.", KnowledgeStorageKind.EvidenceAggregate, Classification.Internal, aggregate: true),
            Seed("governance.risk", "Uncertainty with impact.", KnowledgeStorageKind.KnowledgeEnvelope, Classification.Confidential),
            Seed("delivery.release", "Release candidate or released state.", KnowledgeStorageKind.KnowledgeEnvelope, Classification.Internal),
            Seed("delivery.deployment", "External deployment record.", KnowledgeStorageKind.KnowledgeEnvelope, Classification.Internal),
            Seed("operation.incident", "Operational event or incident.", KnowledgeStorageKind.KnowledgeEnvelope, Classification.Confidential),
            Seed("learning.lesson", "Educational explanation or retrospective.", KnowledgeStorageKind.KnowledgeEnvelope, Classification.Internal),
            Seed("experience.feedback", "User or stakeholder feedback.", KnowledgeStorageKind.KnowledgeEnvelope, Classification.Internal));
    }

    private static object Seed(
        string typeId,
        string description,
        KnowledgeStorageKind storageKind,
        Classification defaultClassification,
        bool aggregate = false) =>
        new
        {
            TypeId = typeId,
            Owner = CanonicalKnowledgeCatalogue.Owner,
            Description = description,
            StorageKind = storageKind,
            LifecyclePolicy = "independent-review-verification-currency",
            DefaultClassification = defaultClassification,
            IsCanonical = true,
            MigrationStrategy = aggregate
                ? "map-to-existing-aggregate; do-not-duplicate-as-knowledge"
                : "preserve-plan-star-aliases; prefer-canonical-for-new-items",
            CreatedAt = SeededAt,
        };
}
