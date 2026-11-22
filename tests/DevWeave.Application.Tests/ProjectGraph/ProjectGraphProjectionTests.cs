using DevWeave.Application.Abstractions;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Application.ProjectGraph;
using DevWeave.Application.Security;
using DevWeave.Domain.Agents;
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
        var actor = Principal.Create(tenant, "member@graph.test", PrincipalType.Human);
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
        var projection = new ProjectGraphProjection(
            new StaticGraphSource(snapshot),
            CreatePolicy(
                actor,
                ProjectMembership.Grant(tenant, project, actor.Id, ProjectRole.Member)));

        var visible = await projection.BuildAsync(
            new AuthorizedScope(tenant, actor.Id, project, "Inspect graph", "correlation"));

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
        var outsider = Principal.Create(tenant, "outsider@graph.test", PrincipalType.Human);
        var projection = new ProjectGraphProjection(
            new StaticGraphSource(new ProjectGraphSnapshot(DateTimeOffset.UtcNow, [], [])),
            CreatePolicy(outsider));

        await Assert.ThrowsAsync<NotFoundException>(() => projection.BuildAsync(
            new AuthorizedScope(
                tenant,
                outsider.Id,
                project,
                "Inspect graph",
                "correlation")));
    }

    private static ResourcePolicyEngine CreatePolicy(
        Principal actor,
        params ProjectMembership[] memberships) =>
        new(
            new PrincipalStore(actor),
            new ProjectMemberships(memberships),
            new EmptyTenantMemberships(),
            new GrantStore(),
            new ItemStore());

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

    private sealed class PrincipalStore(params Principal[] principals) : IPrincipalRepository
    {
        public Task AddAsync(Principal principal, CancellationToken ct = default) => Task.CompletedTask;

        public Task<Principal?> GetByIdAsync(
            TenantId tenantId,
            PrincipalId id,
            CancellationToken ct = default) =>
            Task.FromResult(principals.SingleOrDefault(candidate =>
                candidate.TenantId == tenantId && candidate.Id == id));

        public Task<IReadOnlyList<Principal>> ListByTenantAndTypeAsync(
            TenantId tenantId,
            PrincipalType type,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Principal>>(
                principals.Where(candidate => candidate.TenantId == tenantId && candidate.Type == type).ToArray());
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

    private sealed class GrantStore : IAgentGrantRepository
    {
        public Task AddAsync(AgentGrant grant, CancellationToken ct = default) => Task.CompletedTask;

        public Task<AgentGrant?> GetByIdAsync(
            TenantId tenantId,
            AgentGrantId id,
            CancellationToken ct = default) =>
            Task.FromResult<AgentGrant?>(null);

        public Task<IReadOnlyList<AgentGrant>> ListForAgentAsync(
            TenantId tenantId,
            PrincipalId agentId,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AgentGrant>>([]);
    }

    private sealed class ItemStore : IKnowledgeItemRepository
    {
        public Task AddAsync(KnowledgeItem item, CancellationToken ct = default) => Task.CompletedTask;

        public Task<KnowledgeItem?> GetByIdAsync(
            TenantId tenantId,
            KnowledgeItemId id,
            CancellationToken ct = default) =>
            Task.FromResult<KnowledgeItem?>(null);

        public Task<IReadOnlyList<KnowledgeItem>> ListByProjectAsync(
            TenantId tenantId,
            ProjectId projectId,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<KnowledgeItem>>([]);

        public Task<IReadOnlyList<KnowledgeItem>> ListByTenantAsync(
            TenantId tenantId,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<KnowledgeItem>>([]);

        public Task<bool> KeyExistsAsync(
            TenantId tenantId,
            ProjectId projectId,
            string key,
            CancellationToken ct = default) =>
            Task.FromResult(false);
    }
}
