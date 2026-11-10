using DevWeave.Application.Abstractions;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;

namespace DevWeave.Application.Agents;

/// <summary>
/// Lists the credentials of an agent principal, tenant-scoped (SRS AGT-002). A cross-tenant agent is
/// indistinguishable from a missing one (deny-by-default).
/// </summary>
public sealed class ListAgentCredentials
{
    private readonly IAgentCredentialRepository _credentials;
    private readonly IPrincipalRepository _principals;

    public ListAgentCredentials(
        IAgentCredentialRepository credentials,
        IPrincipalRepository principals)
    {
        _credentials = credentials;
        _principals = principals;
    }

    public async Task<IReadOnlyList<AgentCredential>> ExecuteAsync(
        TenantId tenantId,
        PrincipalId agentId,
        CancellationToken ct = default)
    {
        _ = await _principals.GetByIdAsync(tenantId, agentId, ct)
            ?? throw new NotFoundException($"Principal '{agentId}' was not found within tenant '{tenantId}'.");

        return await _credentials.ListForAgentAsync(tenantId, agentId, ct);
    }
}