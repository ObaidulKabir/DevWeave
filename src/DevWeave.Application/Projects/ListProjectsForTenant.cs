using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Projects;

public sealed class ListProjectsForTenant
{
    private readonly IProjectRepository _projects;

    public ListProjectsForTenant(IProjectRepository projects)
    {
        _projects = projects;
    }

    public Task<IReadOnlyList<Project>> ExecuteAsync(TenantId tenantId, CancellationToken ct = default) =>
        _projects.ListByTenantAsync(tenantId, ct);
}