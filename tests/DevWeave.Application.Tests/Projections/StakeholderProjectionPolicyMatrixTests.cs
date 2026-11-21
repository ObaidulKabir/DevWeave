using DevWeave.Application.Abstractions;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Application.Projections;
using DevWeave.Application.ProjectGraph;
using DevWeave.Application.Security;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Relations;

namespace DevWeave.Application.Tests.Projections;

public sealed class StakeholderProjectionPolicyMatrixTests
{
    [Fact]
    public async Task Same_canonical_graph_yields_different_authorized_views()
    {
        var harness = Harness.Create();

        var ownerOverview = await harness.Build(harness.Owner, StakeholderProfile.Owner, ProjectionPlanCodes.Overview);
        var investorOverview = await harness.Build(harness.Owner, StakeholderProfile.Investor, ProjectionPlanCodes.Overview);
        var learnerOverview = await harness.Build(harness.Owner, StakeholderProfile.Learner, ProjectionPlanCodes.Overview);
        var userOverview = await harness.Build(harness.Owner, StakeholderProfile.User, ProjectionPlanCodes.Overview);
        var agentOverview = await harness.Build(harness.Agent, StakeholderProfile.Agent, ProjectionPlanCodes.Overview);
        var memberOverview = await harness.Build(harness.Member, StakeholderProfile.Owner, ProjectionPlanCodes.Overview);

        Assert.Equal(harness.Canonical.Nodes.Count, ownerOverview.Nodes.Count);
        Assert.Contains(ownerOverview.Nodes, node => node.Resource == harness.SecretRequirement);
        Assert.Contains(ownerOverview.SummaryInputs, resource => resource == harness.SecretRequirement);

        AssertRestrictedOmitted(investorOverview);
        Assert.DoesNotContain(investorOverview.Nodes, node => node.Resource == harness.WorkPackage);
        Assert.DoesNotContain(investorOverview.Nodes, node => node.Resource == harness.Activity);
        Assert.Contains(investorOverview.Nodes, node => node.Resource == harness.PublicRequirement);
        Assert.Contains(investorOverview.Nodes, node => node.Resource == harness.WorkItem);

        AssertRestrictedOmitted(learnerOverview);
        Assert.DoesNotContain(learnerOverview.Nodes, node => node.Resource == harness.WorkItem);
        Assert.Contains(learnerOverview.Nodes, node => node.Resource == harness.InternalRequirement);

        AssertRestrictedOmitted(userOverview);
        Assert.DoesNotContain(userOverview.Nodes, node => node.Resource == harness.InternalRequirement);
        Assert.Contains(userOverview.Nodes, node => node.Resource == harness.PublicRequirement);

        AssertRestrictedOmitted(agentOverview);
        Assert.DoesNotContain(agentOverview.Nodes, node => node.Resource == harness.ProjectNode);
        Assert.Contains(agentOverview.Nodes, node => node.Resource == harness.WorkPackage);

        AssertRestrictedOmitted(memberOverview);
        Assert.Contains(memberOverview.Nodes, node => node.Resource == harness.InternalRequirement);

        Assert.Equal(harness.Canonical.Nodes.Count, harness.Source.Snapshot.Nodes.Count);
        Assert.Contains(harness.Source.Snapshot.Nodes, node => node.Resource == harness.SecretRequirement);
    }

    [Fact]
    public async Task Traceability_does_not_walk_into_restricted_nodes()
    {
        var harness = Harness.Create();

        var ownerTrace = await harness.Build(
            harness.Owner,
            StakeholderProfile.Owner,
            ProjectionPlanCodes.Traceability,
            harness.PublicRequirement);
        var investorTrace = await harness.Build(
            harness.Owner,
            StakeholderProfile.Investor,
            ProjectionPlanCodes.Traceability,
            harness.PublicRequirement);

        Assert.Contains(ownerTrace.Paths, path => path.End == harness.SecretRequirement);
        Assert.Contains(ownerTrace.Paths, path => path.End == harness.InternalRequirement);
        Assert.DoesNotContain(investorTrace.Paths, path => path.End == harness.SecretRequirement);
        Assert.Contains(investorTrace.Paths, path => path.End == harness.InternalRequirement);
        Assert.DoesNotContain(
            investorTrace.Edges,
            edge => edge.From == harness.SecretRequirement || edge.To == harness.SecretRequirement);
        Assert.DoesNotContain(
            investorTrace.Nodes,
            node => node.Resource.Kind == ProjectResourceKind.Activity);
    }

    private static void AssertRestrictedOmitted(StakeholderProjection projection)
    {
        Assert.DoesNotContain(projection.Nodes, node => node.Classification == Classification.Restricted);
        Assert.DoesNotContain(
            projection.Edges,
            edge => edge.From.Kind == ProjectResourceKind.Artifact || edge.To.Kind == ProjectResourceKind.Artifact);
        Assert.DoesNotContain(projection.Counts, count => count.Key.Contains("Restricted", StringComparison.Ordinal));
        Assert.DoesNotContain(
            projection.SummaryInputs,
            resource => resource.Kind == ProjectResourceKind.Artifact);
        Assert.Equal(
            projection.Nodes.Count,
            projection.Counts.Single(count => count.Key == "nodes").Value);
    }

    private sealed class Harness
    {
        public required StakeholderProjectionService Service { get; init; }
        public required StaticGraphSource Source { get; init; }
        public required ProjectGraphSnapshot Canonical { get; init; }
        public required TenantId Tenant { get; init; }
        public required ProjectId Project { get; init; }
        public required Principal Owner { get; init; }
        public required Principal Member { get; init; }
        public required Principal Agent { get; init; }
        public required ProjectResourceRef ProjectNode { get; init; }
        public required ProjectResourceRef PublicRequirement { get; init; }
        public required ProjectResourceRef InternalRequirement { get; init; }
        public required ProjectResourceRef SecretRequirement { get; init; }
        public required ProjectResourceRef WorkItem { get; init; }
        public required ProjectResourceRef WorkPackage { get; init; }
        public required ProjectResourceRef Activity { get; init; }

        public Task<StakeholderProjection> Build(
            Principal actor,
            StakeholderProfile profile,
            string plan,
            ProjectResourceRef? start = null) =>
            Service.BuildAsync(
                new AuthorizedScope(Tenant, actor.Id, Project, $"{profile} {plan}", "projection-matrix"),
                profile,
                plan,
                start: start);

        public static Harness Create()
        {
            var tenant = TenantId.New();
            var project = ProjectId.New();
            var owner = Principal.Create(tenant, "owner@projection.test", PrincipalType.Human);
            var member = Principal.Create(tenant, "member@projection.test", PrincipalType.Human);
            var agent = Principal.Create(tenant, "agent@projection.test", PrincipalType.AiAgent);

            var projectNode = ProjectResourceRef.Local(ProjectResourceKind.Project, project.Value);
            var publicReq = ProjectResourceRef.Local(ProjectResourceKind.KnowledgeItem, Guid.NewGuid());
            var internalReq = ProjectResourceRef.Local(ProjectResourceKind.KnowledgeItem, Guid.NewGuid());
            var secretReq = ProjectResourceRef.Local(ProjectResourceKind.KnowledgeItem, Guid.NewGuid());
            var workItem = ProjectResourceRef.Local(ProjectResourceKind.WorkItem, Guid.NewGuid());
            var workPackage = ProjectResourceRef.Local(ProjectResourceKind.WorkPackage, Guid.NewGuid());
            var activity = ProjectResourceRef.Local(ProjectResourceKind.Activity, Guid.NewGuid());
            var evidence = ProjectResourceRef.Local(ProjectResourceKind.Evidence, Guid.NewGuid());
            var secretArtifact = ProjectResourceRef.Local(ProjectResourceKind.Artifact, Guid.NewGuid());
            var now = DateTimeOffset.UtcNow;

            var snapshot = new ProjectGraphSnapshot(
                now,
                [
                    new(projectNode, "Project", Classification.Internal, now),
                    new(publicReq, "Public FR", Classification.Public, now),
                    new(internalReq, "Internal FR", Classification.Internal, now),
                    new(secretReq, "Restricted FR", Classification.Restricted, now),
                    new(workItem, "Implement FR", Classification.Internal, now),
                    new(workPackage, "Package", Classification.Internal, now),
                    new(activity, "Agent run", Classification.Internal, now),
                    new(evidence, "Test evidence", Classification.Internal, now),
                    new(secretArtifact, "Secret diagram", Classification.Restricted, now),
                ],
                [
                    new(projectNode, publicReq, RelationTypeCodes.Contains),
                    new(projectNode, internalReq, RelationTypeCodes.Contains),
                    new(projectNode, secretReq, RelationTypeCodes.Contains),
                    new(publicReq, secretReq, RelationTypeCodes.Satisfies),
                    new(publicReq, internalReq, RelationTypeCodes.Satisfies),
                    new(internalReq, activity, "used_by"),
                    new(workItem, internalReq, RelationTypeCodes.Implements),
                    new(secretReq, secretArtifact, "supported_by"),
                ]);

            var grant = AgentGrant.Propose(
                tenant,
                project,
                agent.Id,
                AgentCapability.ReadKnowledge,
                expiresAt: null,
                Classification.Internal);
            grant.Approve(owner.Id);

            var engine = new ResourcePolicyEngine(
                new PrincipalStore(owner, member, agent),
                new ProjectMemberships(
                    ProjectMembership.Grant(tenant, project, owner.Id, ProjectRole.Owner),
                    ProjectMembership.Grant(tenant, project, member.Id, ProjectRole.Member)),
                new EmptyTenantMemberships(),
                new GrantStore(grant),
                new ItemStore());
            var source = new StaticGraphSource(snapshot);

            return new Harness
            {
                Service = new StakeholderProjectionService(source, engine),
                Source = source,
                Canonical = snapshot,
                Tenant = tenant,
                Project = project,
                Owner = owner,
                Member = member,
                Agent = agent,
                ProjectNode = projectNode,
                PublicRequirement = publicReq,
                InternalRequirement = internalReq,
                SecretRequirement = secretReq,
                WorkItem = workItem,
                WorkPackage = workPackage,
                Activity = activity,
            };
        }
    }

    internal sealed class StaticGraphSource(ProjectGraphSnapshot snapshot) : IProjectGraphSource
    {
        public ProjectGraphSnapshot Snapshot { get; } = snapshot;

        public Task<ProjectGraphSnapshot> LoadAsync(
            TenantId tenantId,
            ProjectId projectId,
            DateTimeOffset asOf,
            CancellationToken ct = default,
            DateTimeOffset? asOfValid = null) =>
            Task.FromResult(Snapshot);
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

    private sealed class ProjectMemberships(params ProjectMembership[] memberships) : IProjectMembershipRepository
    {
        public Task AddAsync(ProjectMembership membership, CancellationToken ct = default) => Task.CompletedTask;

        public Task<ProjectMembership?> GetByIdAsync(
            TenantId tenantId,
            ProjectMembershipId id,
            CancellationToken ct = default) =>
            Task.FromResult(memberships.SingleOrDefault(candidate =>
                candidate.TenantId == tenantId && candidate.Id == id));

        public Task<IReadOnlyList<ProjectMembership>> ListByProjectAsync(
            TenantId tenantId,
            ProjectId projectId,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ProjectMembership>>(
                memberships.Where(candidate => candidate.TenantId == tenantId && candidate.ProjectId == projectId)
                    .ToArray());

        public Task<IReadOnlyList<ProjectMembership>> ListByPrincipalAsync(
            TenantId tenantId,
            PrincipalId principalId,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ProjectMembership>>(
                memberships.Where(candidate =>
                    candidate.TenantId == tenantId && candidate.PrincipalId == principalId)
                    .ToArray());
    }

    private sealed class EmptyTenantMemberships : IMembershipRepository
    {
        public Task AddAsync(Membership membership, CancellationToken ct = default) => Task.CompletedTask;

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

    private sealed class GrantStore(params AgentGrant[] grants) : IAgentGrantRepository
    {
        public Task AddAsync(AgentGrant grant, CancellationToken ct = default) => Task.CompletedTask;

        public Task<AgentGrant?> GetByIdAsync(
            TenantId tenantId,
            AgentGrantId id,
            CancellationToken ct = default) =>
            Task.FromResult(grants.SingleOrDefault(candidate =>
                candidate.TenantId == tenantId && candidate.Id == id));

        public Task<IReadOnlyList<AgentGrant>> ListForAgentAsync(
            TenantId tenantId,
            PrincipalId agentId,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AgentGrant>>(
                grants.Where(candidate => candidate.TenantId == tenantId && candidate.AgentId == agentId).ToArray());
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
