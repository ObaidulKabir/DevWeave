using DevWeave.Application.Abstractions;
using DevWeave.Domain.Activities;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevWeave.Infrastructure.Repositories;

internal sealed class ActivityRepository : IActivityRepository
{
    private readonly DevWeaveDbContext _db;

    public ActivityRepository(DevWeaveDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(Activity activity, CancellationToken ct = default)
    {
        _db.Activities.Add(activity);
        return Task.CompletedTask;
    }

    // Deny-by-default: a cross-tenant activity is indistinguishable from a missing one.
    public Task<Activity?> GetByIdAsync(TenantId tenantId, ActivityId id, CancellationToken ct = default) =>
        _db.Activities
            .Include(a => a.Inputs)
            .Include(a => a.Outputs)
            .SingleOrDefaultAsync(a => a.TenantId == tenantId && a.Id == id, ct);

    public async Task<IReadOnlyList<Activity>> ListForProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default) =>
        await _db.Activities
            .Include(a => a.Inputs)
            .Include(a => a.Outputs)
            .Where(a => a.TenantId == tenantId && a.ProjectId == projectId)
            .OrderBy(a => a.StartedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Activity>> ListForItemAsync(TenantId tenantId, KnowledgeItemId itemId, CancellationToken ct = default) =>
        await _db.Activities
            .Include(a => a.Inputs)
            .Include(a => a.Outputs)
            .Where(a => a.TenantId == tenantId
                && (a.Inputs.Any(i => i.ItemId == itemId) || a.Outputs.Any(o => o.ItemId == itemId)))
            .OrderBy(a => a.StartedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Activity>> ListByActorAsync(TenantId tenantId, PrincipalId actorId, CancellationToken ct = default) =>
        await _db.Activities
            .Include(a => a.Inputs)
            .Include(a => a.Outputs)
            .Where(a => a.TenantId == tenantId && a.Actor == actorId)
            .OrderBy(a => a.StartedAt)
            .ToListAsync(ct);

    // AGT-005: execution deduplication resolves by the correlation id (the context package id) scoped
    // to the same project and agent.
    public Task<Activity?> FindByCorrelationIdAsync(
        TenantId tenantId,
        ProjectId projectId,
        PrincipalId actorId,
        string correlationId,
        CancellationToken ct = default) =>
        _db.Activities
            .Include(a => a.Inputs)
            .Include(a => a.Outputs)
            .SingleOrDefaultAsync(a =>
                a.TenantId == tenantId && a.ProjectId == projectId && a.Actor == actorId && a.CorrelationId == correlationId,
                ct);
}