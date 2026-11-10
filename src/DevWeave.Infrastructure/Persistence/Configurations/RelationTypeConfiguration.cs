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
    /// Seeds exactly the 11 canonical relation types mandated by SRS REL-003, all Active, Forward,
    /// many-to-many and endpoint-unrestricted. Stable Guids make the seed idempotent and diff-stable
    /// across environments.
    /// </summary>
    private static void SeedCanonicalTypes(EntityTypeBuilder<RelationType> builder)
    {
        IReadOnlyCollection<string> unrestricted = Array.Empty<string>();

        builder.HasData(
            new
            {
                Id = new RelationTypeId(new Guid("00000000-0000-0000-0000-000000000001")),
                Code = RelationTypeCodes.Contains,
                Name = "Contains",
                AllowedSourceTypeIds = unrestricted,
                AllowedTargetTypeIds = unrestricted,
                Direction = RelationDirection.Forward,
                Cardinality = RelationCardinality.ManyToMany,
                Status = RelationTypeStatus.Active,
            },
            new
            {
                Id = new RelationTypeId(new Guid("00000000-0000-0000-0000-000000000002")),
                Code = RelationTypeCodes.DependsOn,
                Name = "Depends On",
                AllowedSourceTypeIds = unrestricted,
                AllowedTargetTypeIds = unrestricted,
                Direction = RelationDirection.Forward,
                Cardinality = RelationCardinality.ManyToMany,
                Status = RelationTypeStatus.Active,
            },
            new
            {
                Id = new RelationTypeId(new Guid("00000000-0000-0000-0000-000000000003")),
                Code = RelationTypeCodes.Satisfies,
                Name = "Satisfies",
                AllowedSourceTypeIds = unrestricted,
                AllowedTargetTypeIds = unrestricted,
                Direction = RelationDirection.Forward,
                Cardinality = RelationCardinality.ManyToMany,
                Status = RelationTypeStatus.Active,
            },
            new
            {
                Id = new RelationTypeId(new Guid("00000000-0000-0000-0000-000000000004")),
                Code = RelationTypeCodes.Implements,
                Name = "Implements",
                AllowedSourceTypeIds = unrestricted,
                AllowedTargetTypeIds = unrestricted,
                Direction = RelationDirection.Forward,
                Cardinality = RelationCardinality.ManyToMany,
                Status = RelationTypeStatus.Active,
            },
            new
            {
                Id = new RelationTypeId(new Guid("00000000-0000-0000-0000-000000000005")),
                Code = RelationTypeCodes.Validates,
                Name = "Validates",
                AllowedSourceTypeIds = unrestricted,
                AllowedTargetTypeIds = unrestricted,
                Direction = RelationDirection.Forward,
                Cardinality = RelationCardinality.ManyToMany,
                Status = RelationTypeStatus.Active,
            },
            new
            {
                Id = new RelationTypeId(new Guid("00000000-0000-0000-0000-000000000006")),
                Code = RelationTypeCodes.DerivedFrom,
                Name = "Derived From",
                AllowedSourceTypeIds = unrestricted,
                AllowedTargetTypeIds = unrestricted,
                Direction = RelationDirection.Forward,
                Cardinality = RelationCardinality.ManyToMany,
                Status = RelationTypeStatus.Active,
            },
            new
            {
                Id = new RelationTypeId(new Guid("00000000-0000-0000-0000-000000000007")),
                Code = RelationTypeCodes.GeneratedBy,
                Name = "Generated By",
                AllowedSourceTypeIds = unrestricted,
                AllowedTargetTypeIds = unrestricted,
                Direction = RelationDirection.Forward,
                Cardinality = RelationCardinality.ManyToMany,
                Status = RelationTypeStatus.Active,
            },
            new
            {
                Id = new RelationTypeId(new Guid("00000000-0000-0000-0000-000000000008")),
                Code = RelationTypeCodes.ApprovedBy,
                Name = "Approved By",
                AllowedSourceTypeIds = unrestricted,
                AllowedTargetTypeIds = unrestricted,
                Direction = RelationDirection.Forward,
                Cardinality = RelationCardinality.ManyToMany,
                Status = RelationTypeStatus.Active,
            },
            new
            {
                Id = new RelationTypeId(new Guid("00000000-0000-0000-0000-000000000009")),
                Code = RelationTypeCodes.Supersedes,
                Name = "Supersedes",
                AllowedSourceTypeIds = unrestricted,
                AllowedTargetTypeIds = unrestricted,
                Direction = RelationDirection.Forward,
                Cardinality = RelationCardinality.ManyToMany,
                Status = RelationTypeStatus.Active,
            },
            new
            {
                Id = new RelationTypeId(new Guid("00000000-0000-0000-0000-00000000000a")),
                Code = RelationTypeCodes.References,
                Name = "References",
                AllowedSourceTypeIds = unrestricted,
                AllowedTargetTypeIds = unrestricted,
                Direction = RelationDirection.Forward,
                Cardinality = RelationCardinality.ManyToMany,
                Status = RelationTypeStatus.Active,
            },
            new
            {
                Id = new RelationTypeId(new Guid("00000000-0000-0000-0000-00000000000b")),
                Code = RelationTypeCodes.ConflictsWith,
                Name = "Conflicts With",
                AllowedSourceTypeIds = unrestricted,
                AllowedTargetTypeIds = unrestricted,
                Direction = RelationDirection.Forward,
                Cardinality = RelationCardinality.ManyToMany,
                Status = RelationTypeStatus.Active,
            });
    }
}