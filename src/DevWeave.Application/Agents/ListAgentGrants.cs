using DevWeave.Application.Abstractions;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;

namespace DevWeave.Application.Agents;

/// <summary>
/// Lists every capability grant of an agent principal (proposed, approved, revoked, expired), tenant-scoped,
/// so Owners can review and govern agent access (SRS AGT-002/AGT-013). A cross-tenant agent is
/// indistinguishable from a missing one (deny-by-default).
/// </summary>
public sealed class ListAgentGrants
{
    private readonly IAgentGrantRepository _grants;
    private readonly IPrincipalRepository _principals;

    public ListAgentGrants(IAgentGrantRepository grants, IPrincipalRepository principals)
    {
        _grants = grants;
        _principals = principals;
    }

    public async Task<IReadOnlyList<AgentGrant>> ExecuteAsync(
        TenantId tenantId,
        PrincipalId agentId,
        CancellationToken ct = default)
    {
        _ = await _principals.GetByIdAsync(tenantId, agentId, ct)
            ?? throw new NotFoundException($"Principal '{agentId}' was not found within tenant '{tenantId}'.");

        var grants = await _grants.ListForAgentAsync(tenantId, agentId, ct);
        return grants.OrderByDescending(grant => grant.GrantedAt).ToList();
    }
}
