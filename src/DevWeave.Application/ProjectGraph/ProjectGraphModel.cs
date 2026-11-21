using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.ProjectGraph;

public sealed record ProjectGraphNode(
    ProjectResourceRef Resource,
    string Label,
    Classification Classification,
    DateTimeOffset RecordedAt);

public sealed record ProjectGraphEdge(
    ProjectResourceRef From,
    ProjectResourceRef To,
    string RelationType,
    ProjectResourceRef? Origin = null);

public enum ProjectGraphDirection
{
    Outgoing = 1,
    Incoming = 2,
}

public sealed record ProjectGraphPath(
    ProjectResourceRef Start,
    ProjectResourceRef End,
    IReadOnlyList<ProjectGraphEdge> Edges);

/// <summary>An immutable, payload-free read projection over canonical project aggregates.</summary>
public sealed class ProjectGraphSnapshot
{
    private readonly IReadOnlyDictionary<string, ProjectGraphNode> _nodesByKey;

    public DateTimeOffset AsOf { get; }
    public IReadOnlyList<ProjectGraphNode> Nodes { get; }
    public IReadOnlyList<ProjectGraphEdge> Edges { get; }

    public ProjectGraphSnapshot(
        DateTimeOffset asOf,
        IReadOnlyList<ProjectGraphNode> nodes,
        IReadOnlyList<ProjectGraphEdge> edges)
    {
        var duplicate = nodes
            .GroupBy(node => node.Resource.CanonicalKey, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new ArgumentException(
                $"Project graph contains duplicate node '{duplicate.Key}'.",
                nameof(nodes));

        _nodesByKey = nodes.ToDictionary(
            node => node.Resource.CanonicalKey,
            StringComparer.Ordinal);
        var dangling = edges.FirstOrDefault(edge =>
            !_nodesByKey.ContainsKey(edge.From.CanonicalKey) ||
            !_nodesByKey.ContainsKey(edge.To.CanonicalKey));
        if (dangling is not null)
            throw new ArgumentException(
                $"Project graph edge '{dangling.RelationType}' has a missing endpoint.",
                nameof(edges));

        AsOf = asOf;
        Nodes = nodes.ToArray();
        Edges = edges.ToArray();
    }

    public IReadOnlyList<ProjectGraphPath> Traverse(
        ProjectResourceRef start,
        ProjectGraphDirection direction,
        int maxDepth,
        int maxResults)
    {
        if (!_nodesByKey.ContainsKey(start.CanonicalKey))
            return [];
        if (maxDepth < 1)
            throw new ArgumentOutOfRangeException(nameof(maxDepth));
        if (maxResults < 1)
            throw new ArgumentOutOfRangeException(nameof(maxResults));

        var paths = new List<ProjectGraphPath>();
        var queue = new Queue<ProjectGraphPath>();
        queue.Enqueue(new ProjectGraphPath(start, start, []));

        while (queue.Count > 0 && paths.Count < maxResults)
        {
            var current = queue.Dequeue();
            if (current.Edges.Count >= maxDepth)
                continue;

            var adjacent = Edges
                .Where(edge => direction == ProjectGraphDirection.Outgoing
                    ? edge.From.CanonicalKey == current.End.CanonicalKey
                    : edge.To.CanonicalKey == current.End.CanonicalKey)
                .OrderBy(edge => edge.RelationType, StringComparer.Ordinal)
                .ThenBy(edge => direction == ProjectGraphDirection.Outgoing
                    ? edge.To.CanonicalKey
                    : edge.From.CanonicalKey,
                    StringComparer.Ordinal);

            foreach (var edge in adjacent)
            {
                var next = direction == ProjectGraphDirection.Outgoing ? edge.To : edge.From;
                if (current.Start.CanonicalKey == next.CanonicalKey ||
                    current.Edges.Any(pathEdge =>
                        pathEdge.From.CanonicalKey == next.CanonicalKey ||
                        pathEdge.To.CanonicalKey == next.CanonicalKey))
                    continue;

                var path = new ProjectGraphPath(
                    start,
                    next,
                    current.Edges.Append(edge).ToArray());
                paths.Add(path);
                queue.Enqueue(path);
                if (paths.Count == maxResults)
                    break;
            }
        }

        return paths;
    }
}
