using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Security;

/// <summary>
/// Tenant Owner/Admin may operate on every project in the tenant. Other humans need an active
/// project membership. Used so a second Owner can see agent-created applications.
/// </summary>
public static class TenantProjectAccess
{
    public static bool IsTenantAdministrator(IEnumerable<Membership> rows) =>
        rows.Any(membership =>
            membership.Status == MembershipStatus.Active &&
            membership.Role is MembershipRole.Owner or MembershipRole.Admin);

    public static async Task<bool> IsTenantAdministratorAsync(
        IMembershipRepository tenantMemberships,
        TenantId tenantId,
        PrincipalId actorId,
        CancellationToken ct)
    {
        var rows = await tenantMemberships.ListByPrincipalAsync(tenantId, actorId, ct);
        return IsTenantAdministrator(rows);
    }

    public static async Task<ProjectRole?> ResolveProjectRoleAsync(
        IProjectMembershipRepository projectMemberships,
        TenantId tenantId,
        ProjectId projectId,
        PrincipalId actorId,
        CancellationToken ct,
        IMembershipRepository? tenantMemberships = null)
    {
        if (tenantMemberships is not null &&
            await IsTenantAdministratorAsync(tenantMemberships, tenantId, actorId, ct))
            return ProjectRole.Owner;

        var rows = await projectMemberships.ListByPrincipalAsync(tenantId, actorId, ct);
        return rows.FirstOrDefault(membership =>
            membership.ProjectId == projectId &&
            membership.Status == ProjectMembershipStatus.Active)?.Role;
    }
}
