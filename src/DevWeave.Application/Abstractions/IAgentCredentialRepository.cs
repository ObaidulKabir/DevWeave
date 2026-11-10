using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;

namespace DevWeave.Application.Abstractions;

/// <summary>
/// Storage port for <see cref="AgentCredential"/> aggregates (SRS AGT-002). Reads are tenant-scoped so a
/// cross-tenant credential is indistinguishable from a missing one (deny-by-default).
/// </summary>
public interface IAgentCredentialRepository
{
    Task AddAsync(AgentCredential credential, CancellationToken ct = default);

    Task<AgentCredential?> GetByIdAsync(TenantId tenantId, AgentCredentialId id, CancellationToken ct = default);

    Task<IReadOnlyList<AgentCredential>> ListForAgentAsync(TenantId tenantId, PrincipalId agentId, CancellationToken ct = default);
}