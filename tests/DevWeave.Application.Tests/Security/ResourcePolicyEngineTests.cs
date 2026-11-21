using DevWeave.Application.Abstractions;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Application.Projections;
using DevWeave.Application.Security;
using DevWeave.Contracts.Planned.V1;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Tests.Security;

public sealed class ResourcePolicyEngineTests
{
    [Fact]
    public async Task Owner_role_allows_restricted_read_under_owner_profile()
    {
        var harness = Harness.Create();
        var decision = await harness.Engine.EvaluateAsync(
            harness.OwnerScope(),
            RestrictedRead());

        Assert.Equal("Allow", decision.Outcome);
        Assert.Equal(ResourcePolicyEngine.PolicyVersion, decision.PolicyVersion);
    }

    [Fact]
    public async Task Owner_investor_profile_cannot_see_restricted_even_with_owner_role()
    {
        var harness = Harness.Create();
        var decision = await harness.Engine.EvaluateAsync(
            harness.OwnerScope(),
            RestrictedRead() with { Profile = StakeholderProfile.Investor });

        Assert.Equal("Deny", decision.Outcome);
        Assert.Equal("not-found", decision.Reason);
    }

    [Fact]
    public async Task Member_owner_profile_cannot_elevate_to_restricted()
    {
        var harness = Harness.Create();
        var decision = await harness.Engine.EvaluateAsync(
            harness.MemberScope(),
            RestrictedRead());

        Assert.Equal("Deny", decision.Outcome);
    }

    [Fact]
    public async Task Agent_grant_ceiling_hides_confidential_as_not_found()
    {
        var harness = Harness.Create();
        var decision = await harness.Engine.EvaluateAsync(
            harness.AgentScope(),
            new ResourcePolicyInput(
                "read",
                ProjectResourceKind.KnowledgeItem,
                Classification.Confidential,
                StakeholderProfile.Agent,
                DateTimeOffset.UtcNow));

        Assert.Equal("Deny", decision.Outcome);
        Assert.Equal("not-found", decision.Reason);
    }

    [Fact]
    public async Task Expired_agent_grant_is_indistinguishable_from_missing()
    {
        var harness = Harness.Create(expiredAgentGrant: true);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            harness.Engine.EnsureProjectAccessAsync(harness.AgentScope(), DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task Empty_purpose_is_denied()
    {
        var harness = Harness.Create();
        var decision = await harness.Engine.EvaluateAsync(
            harness.OwnerScope() with { Purpose = " " },
            RestrictedRead());

        Assert.Equal("Deny", decision.Outcome);
        Assert.Equal("purpose-required", decision.Reason);
    }

    [Fact]
    public async Task Viewer_cannot_write_member_cannot_export()
    {
        var harness = Harness.Create();
        var write = await harness.Engine.EvaluateAsync(
            harness.ViewerScope(),
            new ResourcePolicyInput(
                "write",
                ProjectResourceKind.KnowledgeItem,
                Classification.Internal,
                StakeholderProfile.Owner,
                DateTimeOffset.UtcNow));
        var export = await harness.Engine.EvaluateAsync(
            harness.MemberScope(),
            new ResourcePolicyInput(
                "export",
                ProjectResourceKind.KnowledgeItem,
                Classification.Internal,
                StakeholderProfile.Owner,
                DateTimeOffset.UtcNow));

        Assert.Equal("Deny", write.Outcome);
        Assert.Equal("Deny", export.Outcome);
    }

    [Fact]
    public async Task Resource_port_hides_restricted_knowledge_from_members()
    {
        var harness = Harness.Create();
        var allowed = await harness.Engine.EvaluateAsync(
            harness.OwnerScope(),
            "read",
            new ResourceRef(harness.RestrictedItem.Id.Value, null));
        var denied = await harness.Engine.EvaluateAsync(
            harness.MemberScope(),
            "read",
            new ResourceRef(harness.RestrictedItem.Id.Value, null));

        Assert.Equal("Allow", allowed.Outcome);
        Assert.Equal("Deny", denied.Outcome);
        Assert.Equal("not-found", denied.Reason);
    }

    private static ResourcePolicyInput RestrictedRead() =>
        new(
            "read",
            ProjectResourceKind.KnowledgeItem,
            Classification.Restricted,
            StakeholderProfile.Owner,
            DateTimeOffset.UtcNow);

    private sealed class Harness
    {
        public required ResourcePolicyEngine Engine { get; init; }
        public required TenantId Tenant { get; init; }
        public required ProjectId Project { get; init; }
        public required Principal Owner { get; init; }
        public required Principal Member { get; init; }
        public required Principal Viewer { get; init; }
        public required Principal Agent { get; init; }
        public required KnowledgeItem RestrictedItem { get; init; }

        public AuthorizedScope OwnerScope() => Scope(Owner);
        public AuthorizedScope MemberScope() => Scope(Member);
        public AuthorizedScope ViewerScope() => Scope(Viewer);
        public AuthorizedScope AgentScope() => Scope(Agent);

        private AuthorizedScope Scope(Principal actor) =>
            new(Tenant, actor.Id, Project, "Policy evaluation", "policy-test");

        public static Harness Create(bool expiredAgentGrant = false)
        {
            var tenant = TenantId.New();
            var project = ProjectId.New();
            var owner = Principal.Create(tenant, "owner@policy.test", PrincipalType.Human);
            var member = Principal.Create(tenant, "member@policy.test", PrincipalType.Human);
            var viewer = Principal.Create(tenant, "viewer@policy.test", PrincipalType.Human);
            var agent = Principal.Create(tenant, "agent@policy.test", PrincipalType.AiAgent);
            var restricted = KnowledgeItem.Create(
                tenant,
                project,
                "secret-fr",
                "Secret requirement",
                Classification.Restricted,
                RepresentationKind.Text,
                "requirements.functional",
                "1.0",
                owner.Id);

            var principals = new PrincipalStore(owner, member, viewer, agent);
            var projectMemberships = new ProjectMemberships(
                ProjectMembership.Grant(tenant, project, owner.Id, ProjectRole.Owner),
                ProjectMembership.Grant(tenant, project, member.Id, ProjectRole.Member),
                ProjectMembership.Grant(tenant, project, viewer.Id, ProjectRole.Viewer));
            var grant = AgentGrant.Propose(
                tenant,
                project,
                agent.Id,
                AgentCapability.ReadKnowledge,
                expiredAgentGrant ? DateTimeOffset.UtcNow.AddMinutes(-5) : null,
                Classification.Internal);
            grant.Approve(owner.Id);
            var grants = new GrantStore(grant);
            var items = new ItemStore(restricted);
            var engine = new ResourcePolicyEngine(
                principals,
                projectMemberships,
                new EmptyTenantMemberships(),
                grants,
                items);

            return new Harness
            {
                Engine = engine,
                Tenant = tenant,
                Project = project,
                Owner = owner,
                Member = member,
                Viewer = viewer,
                Agent = agent,
                RestrictedItem = restricted,
            };
        }
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

    private sealed class ItemStore(params KnowledgeItem[] items) : IKnowledgeItemRepository
    {
        public Task AddAsync(KnowledgeItem item, CancellationToken ct = default) => Task.CompletedTask;

        public Task<KnowledgeItem?> GetByIdAsync(
            TenantId tenantId,
            KnowledgeItemId id,
            CancellationToken ct = default) =>
            Task.FromResult(items.SingleOrDefault(candidate =>
                candidate.TenantId == tenantId && candidate.Id == id));

        public Task<IReadOnlyList<KnowledgeItem>> ListByProjectAsync(
            TenantId tenantId,
            ProjectId projectId,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<KnowledgeItem>>(
                items.Where(candidate => candidate.TenantId == tenantId && candidate.ProjectId == projectId)
                    .ToArray());

        public Task<IReadOnlyList<KnowledgeItem>> ListByTenantAsync(
            TenantId tenantId,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<KnowledgeItem>>(
                items.Where(candidate => candidate.TenantId == tenantId).ToArray());

        public Task<bool> KeyExistsAsync(
            TenantId tenantId,
            ProjectId projectId,
            string key,
            CancellationToken ct = default) =>
            Task.FromResult(items.Any(candidate =>
                candidate.TenantId == tenantId &&
                candidate.ProjectId == projectId &&
                candidate.Key == key));
    }
}
