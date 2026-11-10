using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Abstractions;

public interface IProjectRepository
{
    Task AddAsync(Project project, CancellationToken ct = default);

    Task<Project?> GetByIdAsync(TenantId tenantId, ProjectId id, CancellationToken ct = default);

    Task<IReadOnlyList<Project>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default);

    Task<bool> KeyExistsAsync(TenantId tenantId, string key, CancellationToken ct = default);
}