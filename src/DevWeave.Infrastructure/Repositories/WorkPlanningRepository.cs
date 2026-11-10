using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevWeave.Infrastructure.Repositories;

internal sealed class WorkPlanningRepository(DevWeaveDbContext db) : IWorkPlanningRepository
{
    public Task AddWorkItemAsync(WorkItem item, CancellationToken ct = default)
    {
        db.WorkItems.Add(item);
        return Task.CompletedTask;
    }

    public Task<WorkItem?> GetWorkItemAsync(TenantId tenantId, ProjectId projectId, WorkItemId id,
        CancellationToken ct = default) =>
        db.WorkItems.SingleOrDefaultAsync(item =>
            item.TenantId == tenantId && item.ProjectId == projectId && item.Id == id, ct);

    public async Task<IReadOnlyList<WorkItem>> ListWorkItemsAsync(TenantId tenantId,
        ProjectId projectId, CancellationToken ct = default) =>
        await db.WorkItems.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId)
            .OrderBy(item => item.Key)
            .ToListAsync(ct);

    public Task<bool> WorkItemKeyExistsAsync(TenantId tenantId, ProjectId projectId,
        string key, CancellationToken ct = default) =>
        db.WorkItems.AnyAsync(item =>
            item.TenantId == tenantId && item.ProjectId == projectId && item.Key == key, ct);

    public Task AddMilestoneAsync(Milestone milestone, CancellationToken ct = default)
    {
        db.Milestones.Add(milestone);
        return Task.CompletedTask;
    }

    public Task<Milestone?> GetMilestoneAsync(TenantId tenantId, ProjectId projectId,
        MilestoneId id, CancellationToken ct = default) =>
        db.Milestones.SingleOrDefaultAsync(milestone =>
            milestone.TenantId == tenantId && milestone.ProjectId == projectId && milestone.Id == id, ct);

    public async Task<IReadOnlyList<Milestone>> ListMilestonesAsync(TenantId tenantId,
        ProjectId projectId, CancellationToken ct = default) =>
        await db.Milestones.AsNoTracking()
            .Where(milestone => milestone.TenantId == tenantId && milestone.ProjectId == projectId)
            .OrderBy(milestone => milestone.DueAt)
            .ThenBy(milestone => milestone.Key)
            .ToListAsync(ct);

    public Task<bool> MilestoneKeyExistsAsync(TenantId tenantId, ProjectId projectId,
        string key, CancellationToken ct = default) =>
        db.Milestones.AnyAsync(milestone =>
            milestone.TenantId == tenantId && milestone.ProjectId == projectId && milestone.Key == key, ct);

    public Task AddMilestoneTransitionAsync(MilestoneTransition transition, CancellationToken ct = default)
    {
        db.MilestoneTransitions.Add(transition);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<MilestoneTransition>> ListMilestoneTransitionsAsync(TenantId tenantId,
        ProjectId projectId, MilestoneId milestoneId, CancellationToken ct = default) =>
        await db.MilestoneTransitions.AsNoTracking()
            .Where(transition => transition.TenantId == tenantId && transition.ProjectId == projectId
                && transition.MilestoneId == milestoneId)
            .OrderBy(transition => transition.Version)
            .ToListAsync(ct);

    public Task AddDependencyAsync(WorkItemDependency dependency, CancellationToken ct = default)
    {
        db.WorkItemDependencies.Add(dependency);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<WorkItemDependency>> ListDependenciesAsync(TenantId tenantId,
        ProjectId projectId, CancellationToken ct = default) =>
        await db.WorkItemDependencies.AsNoTracking()
            .Where(dependency => dependency.TenantId == tenantId && dependency.ProjectId == projectId)
            .ToListAsync(ct);

    public Task AddTransitionsAsync(IReadOnlyList<WorkItemTransition> transitions,
        CancellationToken ct = default)
    {
        db.WorkItemTransitions.AddRange(transitions);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<WorkItemTransition>> ListTransitionsAsync(TenantId tenantId,
        ProjectId projectId, WorkItemId itemId, CancellationToken ct = default) =>
        await db.WorkItemTransitions.AsNoTracking()
            .Where(transition => transition.TenantId == tenantId
                && transition.ProjectId == projectId && transition.WorkItemId == itemId)
            .OrderBy(transition => transition.Version)
            .ThenBy(transition => transition.RecordedAt)
            .ToListAsync(ct);
}
