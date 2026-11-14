using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;

namespace DevWeave.Application.Agents;

/// <summary>
/// Lists AI-agent principals for a tenant so Owners can manage credentials and MCP access.
/// </summary>
public sealed class ListAgents
{
    private readonly IPrincipalRepository _principals;

    public ListAgents(IPrincipalRepository principals)
    {
        _principals = principals;
    }

    public Task<IReadOnlyList<Principal>> ExecuteAsync(TenantId tenantId, CancellationToken ct = default) =>
        _principals.ListByTenantAndTypeAsync(tenantId, PrincipalType.AiAgent, ct);
}
