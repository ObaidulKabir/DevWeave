using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevWeave.Infrastructure.Repositories;

internal sealed class ProjectEventRepository(DevWeaveDbContext db) : IProjectEventRepository
{
    public Task AddAsync(ProjectEvent projectEvent, CancellationToken ct = default)
    {
        db.ProjectEvents.Add(projectEvent);
        return Task.CompletedTask;
    }

    public Task<ProjectEvent?> GetByIdempotencyKeyAsync(
        TenantId tenantId,
        ProjectId projectId,
        string idempotencyKey,
        CancellationToken ct = default) =>
        db.ProjectEvents
            .Include(projectEvent => projectEvent.Inputs)
            .Include(projectEvent => projectEvent.Outputs)
            .SingleOrDefaultAsync(projectEvent =>
                projectEvent.TenantId == tenantId &&
                projectEvent.ProjectId == projectId &&
                projectEvent.IdempotencyKey == idempotencyKey,
                ct);

    public async Task<IReadOnlyList<ProjectEvent>> ListAsync(
        TenantId tenantId,
        ProjectId projectId,
        long afterSequence = 0,
        int limit = 1000,
        CancellationToken ct = default)
    {
        if (afterSequence < 0)
            throw new ArgumentOutOfRangeException(nameof(afterSequence));
        if (limit is < 1 or > 10_000)
            throw new ArgumentOutOfRangeException(nameof(limit), "Event page size must be between 1 and 10,000.");

        return await db.ProjectEvents
            .Include(projectEvent => projectEvent.Inputs)
            .Include(projectEvent => projectEvent.Outputs)
            .Where(projectEvent =>
                projectEvent.TenantId == tenantId &&
                projectEvent.ProjectId == projectId &&
                projectEvent.Sequence > afterSequence)
            .OrderBy(projectEvent => projectEvent.Sequence)
            .Take(limit)
            .ToListAsync(ct);
    }
}
