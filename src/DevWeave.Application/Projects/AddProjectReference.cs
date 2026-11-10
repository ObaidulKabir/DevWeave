using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Projects;

public sealed class AddProjectReference
{
    private readonly IProjectRepository _projects;
    private readonly IProjectReferenceRepository _references;
    private readonly IUnitOfWork _unitOfWork;

    public AddProjectReference(
        IProjectRepository projects,
        IProjectReferenceRepository references,
        IUnitOfWork unitOfWork)
    {
        _projects = projects;
        _references = references;
        _unitOfWork = unitOfWork;
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
}