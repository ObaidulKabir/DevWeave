namespace DevWeave.Domain.Projects;

/// <summary>
/// Canonical resource kinds used by the project graph, event journal and immutable baseline
/// manifests. The kind is part of identity so equal GUIDs from different aggregates cannot collide.
/// </summary>
public enum ProjectResourceKind
{
    Project = 1,
    KnowledgeItem = 2,
    KnowledgeRevision = 3,
    Relationship = 4,
    WorkItem = 5,
    Activity = 6,
    Evidence = 7,
    Principal = 8,
    Artifact = 9,
    PlanBaseline = 10,
    WorkPackage = 11,
    ChangeSet = 12,
    Release = 13,
    Deployment = 14,
    CodebaseTarget = 15,
    ExternalReference = 16,
    ProjectEvent = 17,
}

/// <summary>
/// A type-qualified, optionally revision-pinned reference. Identifiers are strings so external
/// systems can be represented without manufacturing local aggregate IDs; local references use the
/// canonical "D" GUID format.
/// </summary>
public sealed record ProjectResourceRef
{
    public ProjectResourceKind Kind { get; }
    public string Identifier { get; }
    public string? RevisionIdentifier { get; }

    public ProjectResourceRef(
        ProjectResourceKind kind,
        string identifier,
        string? revisionIdentifier = null)
    {
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentException("A project resource identifier is required.", nameof(identifier));
        if (revisionIdentifier is not null && string.IsNullOrWhiteSpace(revisionIdentifier))
            throw new ArgumentException("A revision identifier cannot be empty.", nameof(revisionIdentifier));

        Kind = kind;
        Identifier = identifier.Trim();
        RevisionIdentifier = revisionIdentifier?.Trim();
    }

    public static ProjectResourceRef Local(
        ProjectResourceKind kind,
        Guid identifier,
        Guid? revisionIdentifier = null)
    {
        if (identifier == Guid.Empty)
            throw new ArgumentException("A local project resource identifier cannot be empty.", nameof(identifier));
        if (revisionIdentifier == Guid.Empty)
            throw new ArgumentException("A local revision identifier cannot be empty.", nameof(revisionIdentifier));

        return new ProjectResourceRef(
            kind,
            identifier.ToString("D"),
            revisionIdentifier?.ToString("D"));
    }

    public string CanonicalKey =>
        $"{Kind}:{Identifier}:{RevisionIdentifier ?? string.Empty}";
}
