using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Projects;

public sealed class GrantProjectMembership
{
    private readonly IProjectRepository _projects;
    private readonly IPrincipalRepository _principals;
    private readonly IProjectMembershipRepository _memberships;
    private readonly IUnitOfWork _unitOfWork;

    public GrantProjectMembership(
        IProjectRepository projects,
        IPrincipalRepository principals,
        IProjectMembershipRepository memberships,
        IUnitOfWork unitOfWork)
    {
        _projects = projects;
        _principals = principals;
        _memberships = memberships;
        _unitOfWork = unitOfWork;
    }

    public async Task<ProjectMembership> ExecuteAsync(
        TenantId tenantId,
        ProjectId projectId,
        PrincipalId principalId,
        ProjectRole role,
        CancellationToken ct = default)
    {
        // Deny-by-default: the project must be resolvable inside the tenant scope.
        var project = await _projects.GetByIdAsync(tenantId, projectId, ct)
            ?? throw new NotFoundException($"Project '{projectId}' was not found within tenant '{tenantId}'.");

        // Deny-by-default: a cross-tenant principal is indistinguishable from a missing one.
        var principal = await _principals.GetByIdAsync(tenantId, principalId, ct)
            ?? throw new NotFoundException($"Principal '{principalId}' was not found within tenant '{tenantId}'.");

        var membership = ProjectMembership.Grant(tenantId, project.Id, principal.Id, role);

        await _memberships.AddAsync(membership, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return membership;
    }
}