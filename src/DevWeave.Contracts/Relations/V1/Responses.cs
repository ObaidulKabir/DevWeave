using DevWeave.Domain.Relations;

namespace DevWeave.Contracts.Relations.V1;

public sealed record RelationshipResponse(
    Guid Id,
    Guid TenantId,
    Guid ProjectId,
    Guid SourceItemId,
    Guid TargetItemId,
    string RelationTypeCode,
    RelationshipStatus Status,
    DateTimeOffset CreatedAt,
    Guid CreatedBy);

public sealed record RelationTypeResponse(
    Guid Id,
    string Code,
    string Name,
    IReadOnlyCollection<string> AllowedSourceTypeIds,
    IReadOnlyCollection<string> AllowedTargetTypeIds,
    RelationDirection Direction,
    RelationCardinality Cardinality,
    RelationTypeStatus Status);

/// <summary>
/// A single explained traversal hop, normalized to the walk direction (SRS REL-005).
/// </summary>
public sealed record TraversalEdgeResponse(
    Guid RelationshipId,
    Guid FromItemId,
    Guid ToItemId,
    string RelationTypeCode);

/// <summary>
/// An ordered, explainable path from the start item to <see cref="EndItemId"/> (SRS REL-005).
/// </summary>
public sealed record TraversalPathResponse(
    Guid StartItemId,
    Guid EndItemId,
    int Depth,
    IReadOnlyList<TraversalEdgeResponse> Edges);

/// <summary>
/// The bounded traversal result (SRS REL-004), reporting discovered paths and whether the result-set
/// limit truncated a larger neighbourhood.
/// </summary>
public sealed record TraversalResponse(
    IReadOnlyList<TraversalPathResponse> Paths,
    bool Truncated);

/// <summary>
/// A structured relationship error body with a machine-readable code (BR-007, REL-002, SCH-007,
/// REL-007). Maps to HTTP 409 for detectable conflicts and HTTP 422 for rule violations.
/// </summary>
public sealed record RelationshipError(string Code, string Message);