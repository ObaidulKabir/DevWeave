using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;

namespace DevWeave.Application.Agents;

/// <summary>
/// Central policy gate for governing agent grants and credentials (SRS AGT-013). Only a human principal
/// holding an active Owner or Admin membership may approve, revoke, disable or rotate agent resources.
/// Fail-closed: non-human principals and principals without an authoritative membership are denied.
/// </summary>
internal static class AgentAuthority
{
    public static async Task EnsureHumanAdminAsync(
        IPrincipalRepository principals,
        IMembershipRepository memberships,
        TenantId tenantId,
        PrincipalId actorId,
        string action,
        CancellationToken ct = default)
    {
        var actor = await principals.GetByIdAsync(tenantId, actorId, ct)
            ?? throw new NotFoundException($"Principal '{actorId}' was not found within tenant '{tenantId}'.");

        if (actor.Type != PrincipalType.Human)
            throw new UnauthorizedTenantException($"Principal '{actorId}' is not a human and cannot {action}.");

        var actorMemberships = await memberships.ListByPrincipalAsync(tenantId, actorId, ct);
        var isAuthority = actorMemberships.Any(m =>
            m.Status == MembershipStatus.Active &&
            m.Role is MembershipRole.Owner or MembershipRole.Admin);

        if (!isAuthority)
            throw new UnauthorizedTenantException($"Principal '{actorId}' lacks an active Owner/Admin membership for tenant '{tenantId}'.");
    }
}