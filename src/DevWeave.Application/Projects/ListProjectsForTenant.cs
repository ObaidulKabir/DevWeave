using DevWeave.Application.Abstractions;
using DevWeave.Application.Security;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Projects;

public sealed class ListProjectsForTenant
{
    private readonly IProjectRepository _projects;
    private readonly IProjectMembershipRepository? _projectMemberships;
    private readonly IMembershipRepository? _tenantMemberships;

    public ListProjectsForTenant(
        IProjectRepository projects,
        IProjectMembershipRepository? projectMemberships = null,
        IMembershipRepository? tenantMemberships = null)
    {
        _projects = projects;
        _projectMemberships = projectMemberships;
        _tenantMemberships = tenantMemberships;
    }

    public Task<IReadOnlyList<Project>> ExecuteAsync(TenantId tenantId, CancellationToken ct = default) =>
        _projects.ListByTenantAsync(tenantId, ct);

    public async Task<IReadOnlyList<Project>> ExecuteAsync(
        TenantId tenantId,
        PrincipalId principalId,
        CancellationToken ct = default)
    {
        var projects = await _projects.ListByTenantAsync(tenantId, ct);
        if (principalId.Value == Guid.Empty)
            return projects;

        if (_tenantMemberships is not null &&
            await TenantProjectAccess.IsTenantAdministratorAsync(_tenantMemberships, tenantId, principalId, ct))
            return projects;

        if (_projectMemberships is null)
            return projects;

        var memberships = await _projectMemberships.ListByPrincipalAsync(tenantId, principalId, ct);
        var visibleProjectIds = memberships
            .Where(m => m.Status == ProjectMembershipStatus.Active)
            .Select(m => m.ProjectId)
            .ToHashSet();

        return projects
            .Where(project => project.CreatedBy == principalId || visibleProjectIds.Contains(project.Id))
            .ToList();
    }
}
