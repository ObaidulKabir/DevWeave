namespace DevWeave.Domain.Relations;

/// <summary>
/// The canonical relation-type codes mandated by SRS REL-003. These are seeded as Active platform
/// configuration in the Infrastructure layer; the domain exposes the authoritative code list so both
/// the domain tests and the seeding logic derive from a single source of truth.
/// </summary>
public static class RelationTypeCodes
{
    public const string Contains = "contains";
    public const string DependsOn = "depends_on";
    public const string Satisfies = "satisfies";
    public const string Implements = "implements";
    public const string Validates = "validates";
    public const string DerivedFrom = "derived_from";
    public const string GeneratedBy = "generated_by";
    public const string ApprovedBy = "approved_by";
    public const string Supersedes = "supersedes";
    public const string References = "references";
    public const string ConflictsWith = "conflicts_with";

    public static readonly IReadOnlyList<string> Canonical =
    [
        Contains,
        DependsOn,
        Satisfies,
        Implements,
        Validates,
        DerivedFrom,
        GeneratedBy,
        ApprovedBy,
        Supersedes,
        References,
        ConflictsWith,
    ];
}