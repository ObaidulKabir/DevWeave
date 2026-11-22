using DevWeave.Domain.Relations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DevWeave.Infrastructure.Persistence.Configurations;

internal sealed class RelationTypeConfiguration : IEntityTypeConfiguration<RelationType>
{
    private static readonly ValueConverter<IReadOnlyCollection<string>, string[]> EndpointTypesConverter =
        new(
            ids => ids.ToArray(),
            value => (IReadOnlyCollection<string>)value);

    private static readonly ValueComparer<IReadOnlyCollection<string>> EndpointTypesComparer =
        new(
            (left, right) =>
                ReferenceEquals(left, right) ||
                (left != null && right != null && left.SequenceEqual(right, StringComparer.Ordinal)),
            ids => ids.Aggregate(17, (hash, id) => HashCode.Combine(hash, StringComparer.Ordinal.GetHashCode(id))),
            ids => ids.ToArray());

    // Appendix B semantics with transitional plan.* / design.* aliases for Phase 2 migration.
    private static readonly string[] DesignAndSpecificationSources =
    [
        "architecture.decision",
        "architecture.component",
        "architecture.interface",
        "specification.technical",
        "design.decision",
        "design.component",
        "plan.design",
        "plan.decision",
        "plan.specification",
        "plan.architecture",
    ];

    private static readonly string[] RequirementTargets =
    [
        "requirements.functional",
        "requirements.nonfunctional",
        "requirements.constraint",
        "plan.requirement",
        "plan.constraint",
        "plan.acceptance",
        "plan.criterion",
    ];

    private static readonly string[] ImplementationSources =
    [
        "architecture.component",
        "architecture.interface",
        "delivery.release",
        "delivery.deployment",
        "plan.implementation",
        "plan.component",
        "plan.module",
    ];

    private static readonly string[] ImplementationTargets =
    [
        "requirements.functional",
        "requirements.nonfunctional",
        "requirements.constraint",
        "specification.technical",
        "plan.requirement",
        "plan.constraint",
        "plan.specification",
    ];

    private static readonly string[] ValidationSources =
    [
        "learning.lesson",
        "experience.feedback",
        "plan.test",
        "plan.test-case",
        "plan.verification",
        "plan.evidence",
        "verification.test-case",
    ];

    public void Configure(EntityTypeBuilder<RelationType> builder)
    {
        builder.ToTable("relation_types");

        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.Id)
            .HasConversion(
                id => id.Value,
                value => new RelationTypeId(value))
            .ValueGeneratedNever();

        builder.Property(rt => rt.Code)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(rt => rt.Name)
            .IsRequired()
            .HasMaxLength(200);

        // Endpoint-type whitelists are immutable, order-insensitive sets persisted as PostgreSQL
        // text[] (SRS REL-002). An empty array is a wildcard for that endpoint.
        builder.Property(rt => rt.AllowedSourceTypeIds)
            .HasConversion(EndpointTypesConverter, EndpointTypesComparer)
            .HasColumnType("text[]");

        builder.Property(rt => rt.AllowedTargetTypeIds)
            .HasConversion(EndpointTypesConverter, EndpointTypesComparer)
            .HasColumnType("text[]");

        builder.Property(rt => rt.Direction)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(rt => rt.Cardinality)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(rt => rt.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.HasIndex(rt => rt.Code)
            .IsUnique();

        SeedCanonicalTypes(builder);
    }

    /// <summary>
    /// Seeds exactly the 11 canonical relation types mandated by SRS REL-003. Semantic types carry
    /// Appendix B endpoint allowlists; structural/provenance types remain unrestricted.
    /// </summary>
    private static void SeedCanonicalTypes(EntityTypeBuilder<RelationType> builder)
    {
        IReadOnlyCollection<string> unrestricted = Array.Empty<string>();

        builder.HasData(
            Seed(1, RelationTypeCodes.Contains, "Contains", unrestricted, unrestricted),
            Seed(2, RelationTypeCodes.DependsOn, "Depends On", unrestricted, unrestricted),
            Seed(3, RelationTypeCodes.Satisfies, "Satisfies", DesignAndSpecificationSources, RequirementTargets),
            Seed(4, RelationTypeCodes.Implements, "Implements", ImplementationSources, ImplementationTargets),
            Seed(5, RelationTypeCodes.Validates, "Validates", ValidationSources, RequirementTargets),
            Seed(6, RelationTypeCodes.DerivedFrom, "Derived From", unrestricted, unrestricted),
            Seed(7, RelationTypeCodes.GeneratedBy, "Generated By", unrestricted, unrestricted),
            Seed(8, RelationTypeCodes.ApprovedBy, "Approved By", unrestricted, unrestricted),
            Seed(9, RelationTypeCodes.Supersedes, "Supersedes", unrestricted, unrestricted),
            Seed(10, RelationTypeCodes.References, "References", unrestricted, unrestricted),
            Seed(11, RelationTypeCodes.ConflictsWith, "Conflicts With", unrestricted, unrestricted));
    }

    private static object Seed(
        int ordinal,
        string code,
        string name,
        IReadOnlyCollection<string> sources,
        IReadOnlyCollection<string> targets) =>
        new
        {
            // Hex so ordinals 10/11 stay 000...000a / 000...000b (matches the original REL-003 seed rows).
            Id = new RelationTypeId(new Guid($"00000000-0000-0000-0000-{ordinal:x12}")),
            Code = code,
            Name = name,
            AllowedSourceTypeIds = sources,
            AllowedTargetTypeIds = targets,
            Direction = RelationDirection.Forward,
            Cardinality = RelationCardinality.ManyToMany,
            Status = RelationTypeStatus.Active,
        };
}
