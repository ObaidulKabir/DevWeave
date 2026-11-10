using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Lifecycle;

internal static class ProjectWorkAuthorization
{
    public static async Task EnsureCanReadAsync(IPrincipalRepository principals,
        IProjectMembershipRepository memberships, TenantId tenantId, ProjectId projectId,
        PrincipalId actorId, CancellationToken ct)
    {
        var principal = await principals.GetByIdAsync(tenantId, actorId, ct);
        if (principal is null || !principal.IsActive)
            throw new NotFoundException($"Project '{projectId}' was not found within tenant '{tenantId}'.");
        var rows = await memberships.ListByPrincipalAsync(tenantId, actorId, ct);
        if (!rows.Any(candidate => candidate.ProjectId == projectId
            && candidate.Status == ProjectMembershipStatus.Active))
            throw new NotFoundException($"Project '{projectId}' was not found within tenant '{tenantId}'.");
    }

    public static async Task EnsureMemberAsync(IProjectMembershipRepository memberships,
        TenantId tenantId, ProjectId projectId, PrincipalId actorId, bool reviewerOnly,
        CancellationToken ct)
    {
        var rows = await memberships.ListByPrincipalAsync(tenantId, actorId, ct);
        var member = rows.FirstOrDefault(candidate =>
            candidate.ProjectId == projectId && candidate.Status == ProjectMembershipStatus.Active);
        if (member is null ||
            (reviewerOnly && member.Role is not (ProjectRole.Owner or ProjectRole.Admin)) ||
            (!reviewerOnly && member.Role == ProjectRole.Viewer))
        {
            // Hide project membership and resource existence from non-members.
            throw new NotFoundException($"Project '{projectId}' was not found within tenant '{tenantId}'.");
        }
    }
}
