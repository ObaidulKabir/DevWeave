using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Relations;

namespace DevWeave.Application.Relations;

/// <summary>
/// Bounded breadth-first traversal over the relationship graph (SRS REL-004, REL-005). The BFS lives
/// here, in the Application layer, over the <see cref="IRelationshipRepository"/> port; nothing is
/// pushed down into SQL. Results are explainable paths of ordered edges (not a flat set), depth is
/// bounded by <c>maxDepth</c>, and the total path count is bounded by <c>maxResults</c>. Revoked edges
/// are hidden from traversal.
/// </summary>
public sealed class TraverseRelationships
{
    private readonly IRelationshipRepository _relationships;
    private readonly IKnowledgeItemRepository _items;

    public TraverseRelationships(IRelationshipRepository relationships, IKnowledgeItemRepository items)
    {
        _relationships = relationships;
        _items = items;
    }

    public async Task<TraversalResult> ExecuteAsync(
        TenantId tenantId,
        KnowledgeItemId startItemId,
        TraversalDirection direction,
        int maxDepth,
        int maxResults,
        CancellationToken ct = default)
    {
        if (maxDepth < 1)
            throw new ArgumentOutOfRangeException(nameof(maxDepth), "Traversal depth must be at least 1.");
        if (maxResults < 1)
            throw new ArgumentOutOfRangeException(nameof(maxResults), "Traversal result limit must be at least 1.");

        // Deny-by-default: a start item from another tenant is indistinguishable from a missing one.
        _ = await _items.GetByIdAsync(tenantId, startItemId, ct)
            ?? throw new NotFoundException($"Knowledge item '{startItemId}' was not found within tenant '{tenantId}'.");

        var results = new List<TraversalPath>();
        var queue = new Queue<TraversalPath>();
        var truncated = false;

        queue.Enqueue(new TraversalPath(startItemId, startItemId, 0, Array.Empty<TraversalEdge>()));

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (current.Depth >= maxDepth)
                continue;

            var neighbors = await ResolveNeighborsAsync(tenantId, current.EndItemId, direction, ct);

            foreach (var neighbor in neighbors)
            {
                // Simple-path constraint: never re-enter a node already visited on this path.
                if (PathVisitsNode(current, neighbor.ToItemId))
                    continue;

                if (results.Count == maxResults)
                {
                    truncated = true;
                    break;
                }

                var edges = current.Edges.ToList();
                edges.Add(new TraversalEdge(
                    neighbor.Identity.Id,
                    current.EndItemId,
                    neighbor.ToItemId,
                    neighbor.Identity.RelationTypeCode));

                var path = new TraversalPath(
                    startItemId,
                    neighbor.ToItemId,
                    current.Depth + 1,
                    edges);

                results.Add(path);
                queue.Enqueue(path);
            }

            if (truncated)
                break;
        }

        return new TraversalResult(results, truncated);
    }

    private async Task<IReadOnlyList<(Relationship Identity, KnowledgeItemId ToItemId)>> ResolveNeighborsAsync(
        TenantId tenantId,
        KnowledgeItemId itemId,
        TraversalDirection direction,
        CancellationToken ct)
    {
        var relationships = direction == TraversalDirection.Outgoing
            ? await _relationships.ListOutgoingAsync(tenantId, itemId, ct)
            : await _relationships.ListIncomingAsync(tenantId, itemId, ct);

        return relationships
            .Where(r => r.Status == RelationshipStatus.Active)
            .OrderBy(r => r.Id.Value)
            .Select(r => (
                Identity: r,
                ToItemId: direction == TraversalDirection.Outgoing ? r.TargetItemId : r.SourceItemId))
            .ToList();
    }

    private static bool PathVisitsNode(TraversalPath path, KnowledgeItemId node)
    {
        if (path.StartItemId == node)
            return true;

        return path.Edges.Any(e => e.ToItemId == node);
    }
}