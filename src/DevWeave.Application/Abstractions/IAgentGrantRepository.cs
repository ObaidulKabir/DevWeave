using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;

namespace DevWeave.Application.Abstractions;

/// <summary>
/// Storage port for <see cref="AgentGrant"/> aggregates (SRS AGT-002, AGT-013). Reads are tenant-scoped
/// so a cross-tenant grant is indistinguishable from a missing one (deny-by-default).
/// </summary>
public interface IAgentGrantRepository
{
    Task AddAsync(AgentGrant grant, CancellationToken ct = default);

    Task<AgentGrant?> GetByIdAsync(TenantId tenantId, AgentGrantId id, CancellationToken ct = default);

    Task<IReadOnlyList<AgentGrant>> ListForAgentAsync(TenantId tenantId, PrincipalId agentId, CancellationToken ct = default);
}