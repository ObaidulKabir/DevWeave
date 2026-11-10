using DevWeave.Domain.Relations;

namespace DevWeave.Contracts.Relations.V1;

/// <summary>
/// Creates a typed, directed relationship between two knowledge items within one project (SRS REL-001).
/// </summary>
public sealed record CreateRelationshipRequest(
    Guid SourceItemId,
    Guid TargetItemId,
    string RelationTypeCode,
    Guid CreatedBy);

/// <summary>
/// Bounded relationship graph traversal query (SRS REL-004). The traversal direction is relative to
/// the start item; depth and result counts bound the walk.
/// </summary>
public sealed record TraverseRelationshipsRequest(
    TraversalDirection Direction,
    int MaxDepth,
    int MaxResults);

/// <summary>
/// Registers a platform-global relation type (SRS REL-002). Empty endpoint-type lists mean the
/// corresponding endpoint is unrestricted.
/// </summary>
public sealed record RegisterRelationTypeRequest(
    string Code,
    string Name,
    IReadOnlyList<string>? AllowedSourceTypeIds,
    IReadOnlyList<string>? AllowedTargetTypeIds,
    RelationDirection Direction,
    RelationCardinality Cardinality);