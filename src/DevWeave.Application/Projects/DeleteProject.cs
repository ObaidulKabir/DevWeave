using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Projects;

public sealed class DeleteProject
{
    private readonly IProjectRepository _projects;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteProject(IProjectRepository projects, IUnitOfWork unitOfWork)
    {
        _projects = projects;
        _unitOfWork = unitOfWork;
    }

    public async Task<Project> ExecuteAsync(TenantId tenantId, ProjectId id, CancellationToken ct = default)
    {
        var project = await _projects.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Project '{id}' was not found within tenant '{tenantId}'.");

        project.Delete();

        await _unitOfWork.SaveChangesAsync(ct);

        return project;
    }
}