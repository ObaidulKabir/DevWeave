using DevWeave.Application.Abstractions.Planned;
using DevWeave.Application.ProjectGraph;
using DevWeave.Application.Security;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Projections;

public sealed record ProjectionCount(
    string Key,
    decimal Value,
    string Unit,
    string IncludedPopulation,
    string ExcludedPopulation);

public sealed record StakeholderProjection(
    DateTimeOffset AsOf,
    StakeholderProfile Profile,
    string Plan,
    string PolicyVersion,
    IReadOnlyList<ProjectGraphNode> Nodes,
    IReadOnlyList<ProjectGraphEdge> Edges,
    IReadOnlyList<ProjectGraphPath> Paths,
    IReadOnlyList<ProjectionCount> Counts,
    IReadOnlyList<ProjectResourceRef> SummaryInputs);

/// <summary>
/// Named, payload-free query plans over the project graph. Policy is applied as each node is admitted
/// and again before a traversal step; stakeholder copies of canonical rows are never created.
/// </summary>
public sealed class StakeholderProjectionService
{
    private readonly IProjectGraphSource _source;
    private readonly ResourcePolicyEngine _policy;

    public StakeholderProjectionService(IProjectGraphSource source, ResourcePolicyEngine policy)
    {
        _source = source;
        _policy = policy;
    }

    public async Task<StakeholderProjection> BuildAsync(
        AuthorizedScope scope,
        StakeholderProfile profile,
        string plan,
        DateTimeOffset? asOf = null,
        CancellationToken ct = default,
        DateTimeOffset? asOfValid = null,
        ProjectResourceRef? start = null,
        int maxDepth = 3,
        int maxResults = 50)
    {
        if (scope.ProjectId is null)
            throw new ArgumentException("A project-scoped projection requires a project ID.", nameof(scope));
        if (!ProjectionPlanCodes.All.Contains(plan))
            throw new ArgumentException($"Unknown projection plan '{plan}'.", nameof(plan));

        var asOfInstant = asOf ?? DateTimeOffset.UtcNow;
        await _policy.EnsureProjectAccessAsync(scope, asOfInstant, ct);

        var canonical = await _source.LoadAsync(
            scope.TenantId,
            scope.ProjectId,
            asOfInstant,
            ct,
            asOfValid);

        var admitted = new List<ProjectGraphNode>();
        foreach (var node in canonical.Nodes)
        {
            if (!ProjectionPlanCodes.AllowsKind(plan, node.Resource.Kind))
                continue;
            if (!await _policy.AllowsNodeAsync(scope, node, profile, ActionFor(plan), asOfInstant, ct))
                continue;
            admitted.Add(node);
        }

        var visibleKeys = admitted
            .Select(node => node.Resource.CanonicalKey)
            .ToHashSet(StringComparer.Ordinal);
        var edges = canonical.Edges
            .Where(edge =>
                visibleKeys.Contains(edge.From.CanonicalKey) &&
                visibleKeys.Contains(edge.To.CanonicalKey) &&
                PlanAllowsEdge(plan, edge))
            .ToArray();

        var authorized = new ProjectGraphSnapshot(canonical.AsOf, admitted, edges);
        var paths = start is null
            ? Array.Empty<ProjectGraphPath>()
            : authorized.TraverseAllowed(
                start,
                ProjectGraphDirection.Outgoing,
                maxDepth <= 0 ? 3 : Math.Min(maxDepth, 8),
                maxResults <= 0 ? 50 : Math.Min(maxResults, 200),
                node => visibleKeys.Contains(node.Resource.CanonicalKey),
                edge => PlanAllowsEdge(plan, edge));

        return new StakeholderProjection(
            authorized.AsOf,
            profile,
            plan.ToLowerInvariant(),
            ResourcePolicyEngine.PolicyVersion,
            authorized.Nodes,
            authorized.Edges,
            paths,
            BuildCounts(authorized),
            authorized.Nodes.Select(node => node.Resource).ToArray());
    }

    private static string ActionFor(string plan) =>
        string.Equals(plan, ProjectionPlanCodes.Traceability, StringComparison.OrdinalIgnoreCase)
            ? "traverse"
            : "read";

    private static bool PlanAllowsEdge(string plan, ProjectGraphEdge edge) =>
        plan.ToLowerInvariant() switch
        {
            ProjectionPlanCodes.Traceability =>
                ImpactRelationCodes.Closure.Contains(edge.RelationType) ||
                string.Equals(edge.RelationType, "has_revision", StringComparison.Ordinal),
            _ => true,
        };

    private static IReadOnlyList<ProjectionCount> BuildCounts(ProjectGraphSnapshot snapshot)
    {
        const string exclusions =
            "Unauthorized, over-classified, or out-of-profile resources; hidden cardinality is not disclosed";
        var counts = new List<ProjectionCount>
        {
            Count("nodes", snapshot.Nodes.Count, "Graph nodes admitted by resource policy and profile", exclusions),
            Count("edges", snapshot.Edges.Count, "Edges whose endpoints were both admitted", exclusions),
        };

        foreach (var group in snapshot.Nodes.GroupBy(node => node.Resource.Kind).OrderBy(group => group.Key))
        {
            counts.Add(Count(
                $"nodes.{group.Key}",
                group.Count(),
                $"Admitted {group.Key} nodes",
                exclusions));
        }

        foreach (var group in snapshot.Nodes
            .GroupBy(node => node.Classification)
            .OrderBy(group => group.Key))
        {
            counts.Add(Count(
                $"classification.{group.Key}",
                group.Count(),
                $"Admitted nodes classified {group.Key}",
                exclusions));
        }

        return counts;
    }

    private static ProjectionCount Count(string key, int value, string included, string excluded) =>
        new(key, value, "count", included, excluded);
}
