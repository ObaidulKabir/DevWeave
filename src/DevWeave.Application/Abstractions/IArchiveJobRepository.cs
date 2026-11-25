using DevWeave.Domain.Archives;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Abstractions;

public interface IArchiveJobRepository
{
    Task AddAsync(ArchiveJob job, CancellationToken ct = default);

    Task<ArchiveJob?> FindAsync(
        TenantId tenantId,
        ProjectId projectId,
        string kind,
        string idempotencyKey,
        CancellationToken ct = default);
}
