using DevWeave.Application.Abstractions;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Application.Projections;
using DevWeave.Application.Security;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.ProjectGraph;

public interface IProjectGraphSource
{
    Task<ProjectGraphSnapshot> LoadAsync(
        TenantId tenantId,
        ProjectId projectId,
        DateTimeOffset asOf,
        CancellationToken ct = default,
        DateTimeOffset? asOfValid = null);
}

public interface IProjectGraphProjection
{
    Task<ProjectGraphSnapshot> BuildAsync(
        AuthorizedScope scope,
        DateTimeOffset? asOf = null,
        CancellationToken ct = default,
        DateTimeOffset? asOfValid = null);
}

/// <summary>
/// Builds an authorization-filtered graph without copying canonical payloads. Policy is applied to
/// each node before any edge or traversal step can enter the caller-visible projection.
/// </summary>
public sealed class ProjectGraphProjection(
    IProjectGraphSource source,
    ResourcePolicyEngine policy)
    : IProjectGraphProjection
{
    public async Task<ProjectGraphSnapshot> BuildAsync(
        AuthorizedScope scope,
        DateTimeOffset? asOf = null,
        CancellationToken ct = default,
        DateTimeOffset? asOfValid = null)
    {
        if (scope.ProjectId is null)
            throw new ArgumentException("A project-scoped graph projection requires a project ID.", nameof(scope));

        var asOfInstant = asOf ?? DateTimeOffset.UtcNow;
        await policy.EnsureProjectAccessAsync(scope, asOfInstant, ct);

        var snapshot = await source.LoadAsync(
            scope.TenantId,
            scope.ProjectId,
            asOfInstant,
            ct,
            asOfValid);
        var nodes = new List<ProjectGraphNode>();
        foreach (var node in snapshot.Nodes)
        {
            if (await policy.AllowsNodeAsync(
                    scope,
                    node,
                    StakeholderProfile.Owner,
                    "read",
                    asOfInstant,
                    ct))
            {
                nodes.Add(node);
            }
        }

        var visibleKeys = nodes
            .Select(node => node.Resource.CanonicalKey)
            .ToHashSet(StringComparer.Ordinal);
        var edges = snapshot.Edges
            .Where(edge =>
                visibleKeys.Contains(edge.From.CanonicalKey) &&
                visibleKeys.Contains(edge.To.CanonicalKey))
            .ToArray();

        return new ProjectGraphSnapshot(snapshot.AsOf, nodes, edges);
    }
}
