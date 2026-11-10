using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Abstractions;

public interface IProjectReferenceRepository
{
    Task AddAsync(ProjectReference reference, CancellationToken ct = default);

    Task<IReadOnlyList<ProjectReference>> ListByProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default);
}