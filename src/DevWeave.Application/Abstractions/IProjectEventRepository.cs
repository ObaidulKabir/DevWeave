using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Abstractions;

public interface IProjectEventRepository
{
    Task AddAsync(ProjectEvent projectEvent, CancellationToken ct = default);

    Task<ProjectEvent?> GetByIdempotencyKeyAsync(
        TenantId tenantId,
        ProjectId projectId,
        string idempotencyKey,
        CancellationToken ct = default);

    Task<IReadOnlyList<ProjectEvent>> ListAsync(
        TenantId tenantId,
        ProjectId projectId,
        long afterSequence = 0,
        int limit = 1000,
        CancellationToken ct = default);
}
