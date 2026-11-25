using DevWeave.Application.Abstractions;
using DevWeave.Application.Security;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Projects;

public sealed class GetProject
{
    private readonly IProjectRepository _projects;
    private readonly IProjectMembershipRepository? _projectMemberships;
    private readonly IMembershipRepository? _tenantMemberships;

    public GetProject(
        IProjectRepository projects,
        IProjectMembershipRepository? projectMemberships = null,
        IMembershipRepository? tenantMemberships = null)
    {
        _projects = projects;
        _projectMemberships = projectMemberships;
        _tenantMemberships = tenantMemberships;
    }

    public Task<Project?> ExecuteAsync(TenantId tenantId, ProjectId id, CancellationToken ct = default) =>
        _projects.GetByIdAsync(tenantId, id, ct);

    public async Task<Project?> ExecuteForActorAsync(
        TenantId tenantId,
        ProjectId id,
        PrincipalId actorId,
        bool platformAdmin,
        CancellationToken ct = default)
    {
        var project = await _projects.GetByIdAsync(tenantId, id, ct);
        if (project is null || platformAdmin)
            return project;

        if (_tenantMemberships is not null &&
            await TenantProjectAccess.IsTenantAdministratorAsync(_tenantMemberships, tenantId, actorId, ct))
            return project;

        if (project.CreatedBy == actorId)
            return project;

        if (_projectMemberships is null)
            return project;

        var role = await TenantProjectAccess.ResolveProjectRoleAsync(
            _projectMemberships, tenantId, id, actorId, ct);
        return role is null ? null : project;
    }
}