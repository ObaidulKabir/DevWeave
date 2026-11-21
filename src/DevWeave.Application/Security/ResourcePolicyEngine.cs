using DevWeave.Application.Abstractions;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Application.Projections;
using DevWeave.Application.ProjectGraph;
using DevWeave.Contracts.Planned.V1;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Security;

/// <summary>
/// Authorization decision for one resource. Combines principal type, project role or agent grant,
/// classification, resource kind/state, action, purpose, stakeholder profile, and as-of time. Profiles
/// never grant authority and cannot elevate a membership or grant ceiling.
/// </summary>
public sealed record ResourcePolicyInput(
    string Action,
    ProjectResourceKind Kind,
    Classification Classification,
    StakeholderProfile Profile,
    DateTimeOffset AsOf,
    string? State = null);

public sealed class ResourcePolicyEngine : IResourcePolicy
{
    public const string PolicyVersion = "resource-policy-1.0";

    private readonly IPrincipalRepository _principals;
    private readonly IProjectMembershipRepository _projectMemberships;
    private readonly IMembershipRepository _tenantMemberships;
    private readonly IAgentGrantRepository _grants;
    private readonly IKnowledgeItemRepository _items;

    public ResourcePolicyEngine(
        IPrincipalRepository principals,
        IProjectMembershipRepository projectMemberships,
        IMembershipRepository tenantMemberships,
        IAgentGrantRepository grants,
        IKnowledgeItemRepository items)
    {
        _principals = principals;
        _projectMemberships = projectMemberships;
        _tenantMemberships = tenantMemberships;
        _grants = grants;
        _items = items;
    }

    public async Task<Decision> EvaluateAsync(
        AuthorizedScope scope,
        string action,
        ResourceRef resource,
        CancellationToken ct = default)
    {
        if (scope.ProjectId is null)
            return Deny("project-required");
        if (resource.Id == Guid.Empty)
            return Deny("not-found");

        var item = await _items.GetByIdAsync(scope.TenantId, new KnowledgeItemId(resource.Id), ct);
        if (item is null || item.ProjectId != scope.ProjectId)
            return Deny("not-found");

        return await EvaluateAsync(
            scope,
            new ResourcePolicyInput(
                action,
                ProjectResourceKind.KnowledgeItem,
                item.Classification,
                StakeholderProfile.Owner,
                DateTimeOffset.UtcNow,
                item.State.ToString()),
            ct);
    }

    public async Task EnsureProjectAccessAsync(
        AuthorizedScope scope,
        DateTimeOffset asOf,
        CancellationToken ct = default)
    {
        if (scope.ProjectId is null)
            throw new ArgumentException("A project-scoped policy check requires a project ID.", nameof(scope));
        if (string.IsNullOrWhiteSpace(scope.Purpose))
            throw new ArgumentException("A declared purpose is required.", nameof(scope));
        if (await ResolveAccessAsync(scope, asOf, ct) is null)
            throw new NotFoundException("Project was not found.");
    }

    public async Task<Decision> EvaluateAsync(
        AuthorizedScope scope,
        ResourcePolicyInput input,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.Action))
            return Deny("action-required");
        if (scope.ProjectId is null)
            return Deny("project-required");
        if (string.IsNullOrWhiteSpace(scope.Purpose))
            return Deny("purpose-required");

        var access = await ResolveAccessAsync(scope, input.AsOf, ct);
        if (access is null)
            return Deny("not-found");

        var ceiling = Min(access.Ceiling, StakeholderProfileCatalog.MaxClassification(input.Profile));
        if (input.Classification > ceiling)
            return Deny("not-found");

        if (!StakeholderProfileCatalog.AllowsKind(input.Profile, input.Kind))
            return Deny("not-found");

        if (IsWithdrawn(input.State) &&
            (input.Profile != StakeholderProfile.Owner ||
             access.Role is not (ProjectRole.Owner or ProjectRole.Admin)))
            return Deny("not-found");

        if (!ActionAllowed(access, input.Action))
            return Deny("not-found");

        return new Decision("Allow", "authorized", PolicyVersion);
    }

    public Task<bool> AllowsNodeAsync(
        AuthorizedScope scope,
        ProjectGraphNode node,
        StakeholderProfile profile,
        string action,
        DateTimeOffset asOf,
        CancellationToken ct = default) =>
        AllowsAsync(
            scope,
            new ResourcePolicyInput(action, node.Resource.Kind, node.Classification, profile, asOf),
            ct);

    public async Task<bool> AllowsAsync(
        AuthorizedScope scope,
        ResourcePolicyInput input,
        CancellationToken ct = default)
    {
        var decision = await EvaluateAsync(scope, input, ct);
        return string.Equals(decision.Outcome, "Allow", StringComparison.Ordinal);
    }

    private async Task<ResolvedAccess?> ResolveAccessAsync(
        AuthorizedScope scope,
        DateTimeOffset asOf,
        CancellationToken ct)
    {
        var principal = await _principals.GetByIdAsync(scope.TenantId, scope.ActorId, ct);
        if (principal is null || !principal.IsActive)
            return null;

        if (principal.Type == PrincipalType.AiAgent)
        {
            var grants = await _grants.ListForAgentAsync(scope.TenantId, principal.Id, ct);
            var active = grants
                .Where(grant => grant.IsActive(asOf) && grant.CoversProject(scope.ProjectId!))
                .ToArray();
            if (active.Length == 0)
                return null;

            return new ResolvedAccess(
                PrincipalType.AiAgent,
                Role: null,
                Ceiling: active.Max(grant => grant.ClassificationCeiling),
                Capabilities: active.Select(grant => grant.Capability).ToHashSet());
        }

        if (principal.Type != PrincipalType.Human)
            return null;

        var role = await TenantProjectAccess.ResolveProjectRoleAsync(
            _projectMemberships,
            scope.TenantId,
            scope.ProjectId!,
            scope.ActorId,
            ct,
            _tenantMemberships);
        if (role is null)
            return null;

        return new ResolvedAccess(
            PrincipalType.Human,
            role,
            ClassificationAccess.CeilingFor(role.Value),
            Capabilities: null);
    }

    private static bool ActionAllowed(ResolvedAccess access, string action)
    {
        var normalized = action.Trim().ToLowerInvariant();
        if (access.PrincipalType == PrincipalType.AiAgent)
        {
            var capabilities = access.Capabilities!;
            return normalized switch
            {
                "write" or "mutate" => capabilities.Contains(AgentCapability.WriteKnowledge),
                "export" => false,
                "traverse" => capabilities.Contains(AgentCapability.TraverseRelations)
                    || capabilities.Contains(AgentCapability.ReadKnowledge)
                    || capabilities.Contains(AgentCapability.AssembleContext),
                _ => capabilities.Contains(AgentCapability.ReadKnowledge)
                    || capabilities.Contains(AgentCapability.AssembleContext)
                    || capabilities.Contains(AgentCapability.TraverseRelations),
            };
        }

        return normalized switch
        {
            "write" or "mutate" => access.Role is ProjectRole.Owner or ProjectRole.Admin or ProjectRole.Member,
            "export" => access.Role is ProjectRole.Owner or ProjectRole.Admin,
            _ => true,
        };
    }

    private static bool IsWithdrawn(string? state) =>
        state is not null &&
        (state.Equals("Deleted", StringComparison.OrdinalIgnoreCase) ||
         state.Equals("Revoked", StringComparison.OrdinalIgnoreCase) ||
         state.Equals("Archived", StringComparison.OrdinalIgnoreCase));

    private static Classification Min(Classification left, Classification right) =>
        left <= right ? left : right;

    private static Decision Deny(string reason) =>
        new("Deny", reason, PolicyVersion);

    private sealed record ResolvedAccess(
        PrincipalType PrincipalType,
        ProjectRole? Role,
        Classification Ceiling,
        HashSet<AgentCapability>? Capabilities);
}
