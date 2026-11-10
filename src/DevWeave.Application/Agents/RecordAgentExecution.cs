using DevWeave.Application.Abstractions;
using DevWeave.Domain.Activities;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Agents;

/// <summary>
/// Records an agent execution as an append-only provenance <see cref="Activity"/> (SRS AGT-005). The
/// context package id is captured as the correlation id and every package entry is recorded as an exact
/// revision-pinned input, so an execution is reproducibly traceable to the snapshot the agent consumed.
/// </summary>
public sealed class RecordAgentExecution
{
    private readonly IActivityRepository _activities;
    private readonly IContextPackageRepository _packages;
    private readonly IPrincipalRepository _principals;
    private readonly IProjectRepository _projects;
    private readonly IAgentCredentialRepository _credentials;
    private readonly IAgentGrantRepository _grants;
    private readonly IUnitOfWork _unitOfWork;

    public RecordAgentExecution(
        IActivityRepository activities,
        IContextPackageRepository packages,
        IPrincipalRepository principals,
        IProjectRepository projects,
        IAgentCredentialRepository credentials,
        IAgentGrantRepository grants,
        IUnitOfWork unitOfWork)
    {
        _activities = activities;
        _packages = packages;
        _principals = principals;
        _projects = projects;
        _credentials = credentials;
        _grants = grants;
        _unitOfWork = unitOfWork;
    }

    public async Task<Activity> ExecuteAsync(
        TenantId tenantId,
        ProjectId projectId,
        PrincipalId agentId,
        ContextPackageId packageId,
        string purpose,
        ActivityStatus status,
        DateTimeOffset startedAt,
        DateTimeOffset? endedAt,
        CancellationToken ct = default)
    {
        var agent = await _principals.GetByIdAsync(tenantId, agentId, ct)
            ?? throw new NotFoundException($"Principal '{agentId}' was not found within tenant '{tenantId}'.");

        if (agent.Type != PrincipalType.AiAgent)
            throw new ConflictException($"Principal '{agentId}' is not an AI agent.");

        _ = await _projects.GetByIdAsync(tenantId, projectId, ct)
            ?? throw new NotFoundException($"Project '{projectId}' was not found within tenant '{tenantId}'.");

        var package = await _packages.GetByIdAsync(tenantId, packageId, ct)
            ?? throw new NotFoundException($"Context package '{packageId}' was not found within tenant '{tenantId}'.");

        if (package.ProjectId != projectId)
            throw new NotFoundException($"Context package '{packageId}' does not belong to project '{projectId}'.");

        // Execution ownership (AGT-005): an agent may only report against a package assembled for it.
        // A package owned by a different agent is treated as absent rather than disclosed.
        if (package.AgentId != agentId)
            throw new NotFoundException($"Context package '{packageId}' does not belong to agent '{agentId}'.");

        // Current credential validity (AGT-002): the agent must hold an active, unexpired credential.
        var now = DateTimeOffset.UtcNow;
        var credentials = await _credentials.ListForAgentAsync(tenantId, agentId, ct);
        if (!credentials.Any(c => c.IsActive(now)))
            throw new UnauthorizedTenantException($"Agent '{agentId}' has no active credential.");

        // Current grant validity (AGT-002): the agent must still hold an active grant covering the project.
        var grants = await _grants.ListForAgentAsync(tenantId, agentId, ct);
        if (!grants.Any(g => g.IsActive(now) && g.CoversProject(projectId)))
            throw new UnauthorizedTenantException($"Agent '{agentId}' has no active grant for project '{projectId}'.");

        // Execution idempotency (AGT-005): a retry of the same execution for the same agent and package
        // resolves to the already-recorded activity rather than duplicating the audit fact.
        var existing = await _activities.FindByCorrelationIdAsync(
            tenantId, projectId, agentId, package.Id.ToString(), ct);
        if (existing is not null)
            return existing;

        var inputs = package.Entries
            .Select(entry => new ActivityInput(entry.ItemId, entry.RevisionId, entry.Position))
            .ToList();

        var activity = Activity.Create(
            tenantId,
            projectId,
            ActivityType.Generated,
            agent.Id,
            purpose,
            status,
            startedAt,
            endedAt,
            correlationId: package.Id.ToString(),
            reason: null,
            reference: null,
            inputs,
            Array.Empty<ActivityOutput>());

        await _activities.AddAsync(activity, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return activity;
    }
}