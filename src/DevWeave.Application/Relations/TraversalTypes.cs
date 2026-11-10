using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Relations;

namespace DevWeave.Application.Relations;

/// <summary>
/// A single traversal hop from one knowledge item to the next along a relationship, normalized so
/// <see cref="FromItemId"/> and <see cref="ToItemId"/> reflect the walk direction (not necessarily the
/// stored source/target orientation).
/// </summary>
public sealed record TraversalEdge(
    RelationshipId RelationshipId,
    KnowledgeItemId FromItemId,
    KnowledgeItemId ToItemId,
    string RelationTypeCode);

/// <summary>
/// An ordered, explainable relationship path from the start item to <see cref="EndItemId"/>
/// (SRS REL-005). <see cref="Depth"/> is the number of edges; <see cref="Edges"/> are ordered from
/// the start outward.
/// </summary>
public sealed record TraversalPath(
    KnowledgeItemId StartItemId,
    KnowledgeItemId EndItemId,
    int Depth,
    IReadOnlyList<TraversalEdge> Edges);

/// <summary>
/// The result of a bounded traversal (SRS REL-004): the discovered paths plus a flag indicating
/// whether the result-set limit truncated a larger neighbourhood.
/// </summary>
public sealed record TraversalResult(
    IReadOnlyList<TraversalPath> Paths,
    bool Truncated);