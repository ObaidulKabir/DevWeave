using DevWeave.Application.Abstractions;
using DevWeave.Application.Lifecycle;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Projects;

public sealed class AddProjectReference
{
    private readonly IProjectRepository _projects;
    private readonly IProjectReferenceRepository _references;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProjectMembershipRepository? _projectMemberships;
    private readonly IMembershipRepository? _tenantMemberships;

    public AddProjectReference(
        IProjectRepository projects,
        IProjectReferenceRepository references,
        IUnitOfWork unitOfWork,
        IProjectMembershipRepository? projectMemberships = null,
        IMembershipRepository? tenantMemberships = null)
    {
        _projects = projects;
        _references = references;
        _unitOfWork = unitOfWork;
        _projectMemberships = projectMemberships;
        _tenantMemberships = tenantMemberships;
    }

    public async Task<ProjectReference> ExecuteAsync(
        TenantId tenantId,
        ProjectId projectId,
        ProjectReferenceType type,
        string externalId,
        string? url,
        CancellationToken ct = default)
    {
        // Deny-by-default: a project from another tenant is indistinguishable from a missing one.
        var project = await _projects.GetByIdAsync(tenantId, projectId, ct)
            ?? throw new NotFoundException($"Project '{projectId}' was not found within tenant '{tenantId}'.");

        var reference = ProjectReference.Create(tenantId, project.Id, type, externalId, url);

        await _references.AddAsync(reference, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return reference;
    }

    public async Task<ProjectReference> ExecuteForActorAsync(
        TenantId tenantId,
        ProjectId projectId,
        PrincipalId actorId,
        bool platformAdmin,
        ProjectReferenceType type,
        string externalId,
        string? url,
        CancellationToken ct = default)
    {
        if (!platformAdmin)
        {
            if (_projectMemberships is null)
                throw new NotFoundException($"Project '{projectId}' was not found within tenant '{tenantId}'.");
            await ProjectWorkAuthorization.EnsureMemberAsync(
                _projectMemberships, tenantId, projectId, actorId, reviewerOnly: false, ct, _tenantMemberships);
        }

        return await ExecuteAsync(tenantId, projectId, type, externalId, url, ct);
    }
}