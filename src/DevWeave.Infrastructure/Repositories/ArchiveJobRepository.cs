using DevWeave.Application.Abstractions;
using DevWeave.Domain.Archives;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevWeave.Infrastructure.Repositories;

internal sealed class ArchiveJobRepository : IArchiveJobRepository
{
    private readonly DevWeaveDbContext _db;

    public ArchiveJobRepository(DevWeaveDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(ArchiveJob job, CancellationToken ct = default)
    {
        _db.ArchiveJobs.Add(job);
        return Task.CompletedTask;
    }

    public Task<ArchiveJob?> FindAsync(
        TenantId tenantId,
        ProjectId projectId,
        string kind,
        string idempotencyKey,
        CancellationToken ct = default) =>
        _db.ArchiveJobs.SingleOrDefaultAsync(
            job => job.TenantId == tenantId &&
                   job.ProjectId == projectId &&
                   job.Kind == kind &&
                   job.IdempotencyKey == idempotencyKey,
            ct);
}
