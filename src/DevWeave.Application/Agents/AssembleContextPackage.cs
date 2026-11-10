using System.Text;
using DevWeave.Application.Abstractions;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Agents;

/// <summary>
/// Assembles a task-scoped, revision-pinned context package for an AI agent (SRS AGT-004). Retrieval is
/// authorization-first: the agent must hold an approved, unexpired grant that covers the project
/// (AGT-002), otherwise the request is denied without disclosing knowledge. Source-code-bearing content
/// is excluded both by the domain aggregate and by the shared content policy run over the actual
/// revision payload (SRS 7.7, AGT-004) — so code text mislabelled as Text/StructuredData cannot leak
/// into agent context. A repeated request with the same idempotency key returns the already-assembled
/// package rather than a duplicate (AGT-010).
/// </summary>
public sealed class AssembleContextPackage
{
    private const int MaxContextEntries = 100;

    private readonly IContextPackageRepository _packages;
    private readonly IAgentGrantRepository _grants;
    private readonly IPrincipalRepository _principals;
    private readonly IProjectRepository _projects;
    private readonly IKnowledgeItemRepository _items;
    private readonly IKnowledgeRevisionRepository _revisions;
    private readonly IAgentCredentialRepository _credentials;
    private readonly IProjectMembershipRepository _memberships;
    private readonly IContentInspector _inspector;
    private readonly IUnitOfWork _unitOfWork;

    public AssembleContextPackage(
        IContextPackageRepository packages,
        IAgentGrantRepository grants,
        IPrincipalRepository principals,
        IProjectRepository projects,
        IKnowledgeItemRepository items,
        IKnowledgeRevisionRepository revisions,
        IAgentCredentialRepository credentials,
        IProjectMembershipRepository memberships,
        IContentInspector inspector,
        IUnitOfWork unitOfWork)
    {
        _packages = packages;
        _grants = grants;
        _principals = principals;
        _projects = projects;
        _items = items;
        _revisions = revisions;
        _credentials = credentials;
        _memberships = memberships;
        _inspector = inspector;
        _unitOfWork = unitOfWork;
    }

    public async Task<ContextPackage> ExecuteAsync(
        TenantId tenantId,
        ProjectId projectId,
        PrincipalId agentId,
        string taskKey,
        string idempotencyKey,
        IReadOnlyList<KnowledgeItemId> itemIds,
        CancellationToken ct = default)
    {
        var agent = await _principals.GetByIdAsync(tenantId, agentId, ct)
            ?? throw new NotFoundException($"Principal '{agentId}' was not found within tenant '{tenantId}'.");

        if (agent.Type != PrincipalType.AiAgent)
            throw new ConflictException($"Principal '{agentId}' is not an AI agent.");

        _ = await _projects.GetByIdAsync(tenantId, projectId, ct)
            ?? throw new NotFoundException($"Project '{projectId}' was not found within tenant '{tenantId}'.");

        // Authorization before retrieval (AGT-002/AGT-004): deny outright when the agent has no
        // effective grant covering this project, without disclosing knowledge existence or contents.
        // Effective grants also bound the most sensitive classification tier the agent may read (AGT-013).
        var grants = await _grants.ListForAgentAsync(tenantId, agentId, ct);
        var now = DateTimeOffset.UtcNow;
        var authorizedGrants = grants
            .Where(g => g.IsActive(now) && g.CoversProject(projectId) && g.AllowsContextAssembly)
            .ToList();

        if (authorizedGrants.Count == 0)
            throw new UnauthorizedTenantException($"Agent '{agentId}' is not authorized for project '{projectId}'.");

        var ceiling = authorizedGrants.Max(g => g.ClassificationCeiling);

        // Authenticated agent status (AGT-002): the agent must hold at least one active, unexpired credential.
        var credentials = await _credentials.ListForAgentAsync(tenantId, agentId, ct);
        if (!credentials.Any(c => c.IsActive(now)))
            throw new UnauthorizedTenantException($"Agent '{agentId}' has no active credential.");

        // Explicit project admission (AGT-002/PRJ-003): the agent must be an active member of the project,
        // so a tenant-wide grant alone cannot reach a project the agent was never admitted to.
        var memberships = await _memberships.ListByPrincipalAsync(tenantId, agentId, ct);
        if (!memberships.Any(m => m.ProjectId == projectId && m.Status == ProjectMembershipStatus.Active))
            throw new UnauthorizedTenantException($"Agent '{agentId}' is not an active member of project '{projectId}'.");

        var requestedIds = (itemIds ?? Array.Empty<KnowledgeItemId>()).Distinct().ToList();
        if (requestedIds.Count > MaxContextEntries)
            throw new ConflictException($"Context request exceeds the maximum of {MaxContextEntries} items.");

        // AGT-010: a retry with the same idempotency key, scoped to this agent, is deduplicated after
        // authorization so a caller cannot probe for existing packages without holding an effective grant.
        var existing = await _packages.FindByIdempotencyKeyAsync(tenantId, projectId, agentId, idempotencyKey, ct);
        if (existing is not null)
            return existing;

        var entries = new List<ContextPackageEntry>();
        foreach (var itemId in requestedIds)
        {
            var item = await _items.GetByIdAsync(tenantId, itemId, ct);
            if (item is null || item.ProjectId != projectId || item.CurrentRevisionId is null)
                continue;

            // Classification ceiling (AGT-013/§7.6): items above the agent's granted ceiling are excluded.
            if (item.Classification > ceiling)
                continue;

            var revision = await _revisions.GetByIdAsync(tenantId, item.CurrentRevisionId, ct);
            if (revision is null)
                continue;

            // Shared content policy (SRS 7.7, AC-09): the actual revision payload is re-inspected here so
            // source-code-bearing or secret content is excluded from agent context even when the item is
            // labelled Text/StructuredData and would slip past the enum-only domain guard.
            var inspection = await _inspector.InspectAsync(
                string.Empty, "application/json", Encoding.UTF8.GetBytes(revision.Content), ct);
            if (inspection.Verdict != ContentVerdict.Allow)
                continue;

            entries.Add(new ContextPackageEntry(item.Id, revision.Id, 0, item.RepresentationKind));
        }

        var package = ContextPackage.Assemble(tenantId, projectId, taskKey, agentId, idempotencyKey, entries);

        await _packages.AddAsync(package, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return package;
    }
}