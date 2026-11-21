using DevWeave.Application.Abstractions;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Application.ProjectGraph;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Tests.ProjectGraph;

public sealed class ProjectGraphProjectionTests
{
    [Fact]
    public async Task Projection_filters_nodes_before_edges_and_traverses_across_resource_kinds()
    {
        var tenant = TenantId.New();
        var project = ProjectId.New();
        var actor = PrincipalId.New();
        var revision = ProjectResourceRef.Local(ProjectResourceKind.KnowledgeRevision, Guid.NewGuid());
        var activity = ProjectResourceRef.Local(ProjectResourceKind.Activity, Guid.NewGuid());
        var evidence = ProjectResourceRef.Local(ProjectResourceKind.Evidence, Guid.NewGuid());
        var hiddenArtifact = ProjectResourceRef.Local(ProjectResourceKind.Artifact, Guid.NewGuid());
        var snapshot = new ProjectGraphSnapshot(
            DateTimeOffset.UtcNow,
            [
                new(revision, "Requirement revision", Classification.Internal, DateTimeOffset.UtcNow),
                new(activity, "Agent execution", Classification.Internal, DateTimeOffset.UtcNow),
                new(evidence, "Test evidence", Classification.Internal, DateTimeOffset.UtcNow),
                new(hiddenArtifact, "Restricted artifact", Classification.Restricted, DateTimeOffset.UtcNow),
            ],
            [
                new(revision, activity, "used_by"),
                new(activity, evidence, "generated_evidence"),
                new(evidence, hiddenArtifact, "supported_by"),
            ]);
        var memberships = new ProjectMemberships(
            ProjectMembership.Grant(tenant, project, actor, ProjectRole.Member));
        var projection = new ProjectGraphProjection(
            new StaticGraphSource(snapshot),
            memberships,
            new EmptyTenantMemberships());

        var visible = await projection.BuildAsync(
            new AuthorizedScope(tenant, actor, project, "Inspect graph", "correlation"));

        Assert.Equal(3, visible.Nodes.Count);
        Assert.Equal(2, visible.Edges.Count);
        Assert.DoesNotContain(visible.Nodes, node => node.Resource == hiddenArtifact);
        var paths = visible.Traverse(revision, ProjectGraphDirection.Outgoing, 3, 10);
        Assert.Equal(2, paths.Count);
        var crossAggregate = Assert.Single(paths, path => path.End == evidence);
        Assert.Equal(
            new[] { "used_by", "generated_evidence" },
            crossAggregate.Edges.Select(edge => edge.RelationType).ToArray());
    }

    [Fact]
    public async Task Projection_hides_project_existence_from_non_members()
    {
        var tenant = TenantId.New();
        var project = ProjectId.New();
        var projection = new ProjectGraphProjection(
            new StaticGraphSource(new ProjectGraphSnapshot(DateTimeOffset.UtcNow, [], [])),
            new ProjectMemberships(),
            new EmptyTenantMemberships());

        await Assert.ThrowsAsync<NotFoundException>(() => projection.BuildAsync(
            new AuthorizedScope(
                tenant,
                PrincipalId.New(),
                project,
                "Inspect graph",
                "correlation")));
    }

    private sealed class StaticGraphSource(ProjectGraphSnapshot snapshot) : IProjectGraphSource
    {
        public Task<ProjectGraphSnapshot> LoadAsync(
            TenantId tenantId,
            ProjectId projectId,
            DateTimeOffset asOf,
            CancellationToken ct = default,
            DateTimeOffset? asOfValid = null) =>
            Task.FromResult(snapshot);
    }

    private sealed class ProjectMemberships(params ProjectMembership[] memberships)
        : IProjectMembershipRepository
    {
        public Task AddAsync(ProjectMembership membership, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<ProjectMembership?> GetByIdAsync(
            TenantId tenantId,
            ProjectMembershipId id,
            CancellationToken ct = default) =>
            Task.FromResult(memberships.SingleOrDefault(candidate =>
                candidate.TenantId == tenantId &&
                candidate.Id == id));

        public Task<IReadOnlyList<ProjectMembership>> ListByProjectAsync(
            TenantId tenantId,
            ProjectId projectId,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ProjectMembership>>(memberships
                .Where(candidate =>
                    candidate.TenantId == tenantId &&
                    candidate.ProjectId == projectId)
                .ToArray());

        public Task<IReadOnlyList<ProjectMembership>> ListByPrincipalAsync(
            TenantId tenantId,
            PrincipalId principalId,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ProjectMembership>>(memberships
                .Where(candidate =>
                    candidate.TenantId == tenantId &&
                    candidate.PrincipalId == principalId)
                .ToArray());
    }

    private sealed class EmptyTenantMemberships : IMembershipRepository
    {
        public Task AddAsync(Membership membership, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<Membership?> GetByIdAsync(
            TenantId tenantId,
            MembershipId id,
            CancellationToken ct = default) =>
            Task.FromResult<Membership?>(null);

        public Task<IReadOnlyList<Membership>> ListByTenantAsync(
            TenantId tenantId,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Membership>>([]);

        public Task<IReadOnlyList<Membership>> ListByPrincipalAsync(
            TenantId tenantId,
            PrincipalId principalId,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Membership>>([]);
    }
}
