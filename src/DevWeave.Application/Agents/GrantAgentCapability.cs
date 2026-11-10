using DevWeave.Application.Abstractions;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Agents;

/// <summary>
/// Proposes a capability grant for an AI agent (SRS AGT-002). A grant starts in the Proposed state and
/// becomes effective only after human approval (AGT-013). A null project scope means the capability
/// applies tenant-wide.
/// </summary>
public sealed class GrantAgentCapability
{
    private readonly IAgentGrantRepository _grants;
    private readonly IPrincipalRepository _principals;
    private readonly IProjectRepository _projects;
    private readonly IUnitOfWork _unitOfWork;

    public GrantAgentCapability(
        IAgentGrantRepository grants,
        IPrincipalRepository principals,
        IProjectRepository projects,
        IUnitOfWork unitOfWork)
    {
        _grants = grants;
        _principals = principals;
        _projects = projects;
        _unitOfWork = unitOfWork;
    }

    public async Task<AgentGrant> ExecuteAsync(
        TenantId tenantId,
        PrincipalId agentId,
        ProjectId? projectId,
        AgentCapability capability,
        DateTimeOffset? expiresAt,
        Classification classificationCeiling = Classification.Internal,
        CancellationToken ct = default)
    {
        var agent = await _principals.GetByIdAsync(tenantId, agentId, ct)
            ?? throw new NotFoundException($"Principal '{agentId}' was not found within tenant '{tenantId}'.");

        if (agent.Type != PrincipalType.AiAgent)
            throw new ConflictException($"Principal '{agentId}' is not an AI agent.");

        if (projectId is not null)
        {
            _ = await _projects.GetByIdAsync(tenantId, projectId, ct)
                ?? throw new NotFoundException($"Project '{projectId}' was not found within tenant '{tenantId}'.");
        }

        var grant = AgentGrant.Propose(tenantId, projectId, agentId, capability, expiresAt, classificationCeiling);

        await _grants.AddAsync(grant, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return grant;
    }
}