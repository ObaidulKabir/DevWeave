using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Projects;

public sealed class GetProject
{
    private readonly IProjectRepository _projects;

    public GetProject(IProjectRepository projects)
    {
        _projects = projects;
    }

    public Task<Project?> ExecuteAsync(TenantId tenantId, ProjectId id, CancellationToken ct = default) =>
        _projects.GetByIdAsync(tenantId, id, ct);
}