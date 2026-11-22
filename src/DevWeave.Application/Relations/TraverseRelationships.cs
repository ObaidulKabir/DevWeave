using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;
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
    private readonly RelationshipReadAuthorization _authorization;

    public TraverseRelationships(
        IRelationshipRepository relationships,
        RelationshipReadAuthorization authorization)
    {
        _relationships = relationships;
        _authorization = authorization;
    }

    public async Task<TraversalResult> ExecuteAsync(
        TenantId tenantId,
        KnowledgeItemId startItemId,
        PrincipalId actorId,
        TraversalDirection direction,
        int maxDepth,
        int maxResults,
        CancellationToken ct = default)
    {
        if (maxDepth < 1)
            throw new ArgumentOutOfRangeException(nameof(maxDepth), "Traversal depth must be at least 1.");
        if (maxResults < 1)
            throw new ArgumentOutOfRangeException(nameof(maxResults), "Traversal result limit must be at least 1.");

        // Deny-by-default: missing, unauthorized and over-classified start items are indistinguishable.
        var (_, role) = await _authorization.RequireVisibleItemAsync(
            tenantId,
            startItemId,
            actorId,
            ct);

        var results = new List<TraversalPath>();
        var queue = new Queue<TraversalPath>();
        var truncated = false;

        queue.Enqueue(new TraversalPath(startItemId, startItemId, 0, Array.Empty<TraversalEdge>()));

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (current.Depth >= maxDepth)
                continue;

            var neighbors = await ResolveNeighborsAsync(tenantId, current.EndItemId, role, direction, ct);

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
        ProjectRole role,
        TraversalDirection direction,
        CancellationToken ct)
    {
        var relationships = direction == TraversalDirection.Outgoing
            ? await _relationships.ListOutgoingAsync(tenantId, itemId, ct)
            : await _relationships.ListIncomingAsync(tenantId, itemId, ct);

        var candidates = relationships
            .Where(r => r.Status == RelationshipStatus.Active)
            .OrderBy(r => r.Id.Value)
            .ToList();

        var visible = new List<(Relationship Identity, KnowledgeItemId ToItemId)>();
        foreach (var relationship in candidates)
        {
            if (!await _authorization.IsRelationshipVisibleAsync(tenantId, relationship, role, ct))
                continue;

            visible.Add((
                relationship,
                direction == TraversalDirection.Outgoing
                    ? relationship.TargetItemId
                    : relationship.SourceItemId));
        }

        return visible;
    }

    private static bool PathVisitsNode(TraversalPath path, KnowledgeItemId node)
    {
        if (path.StartItemId == node)
            return true;

        return path.Edges.Any(e => e.ToItemId == node);
    }
}