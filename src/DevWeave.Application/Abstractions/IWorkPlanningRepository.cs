using DevWeave.Domain.Identity;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Abstractions;

public interface IWorkPlanningRepository
{
    Task AddWorkItemAsync(WorkItem item, CancellationToken ct = default);
    Task<WorkItem?> GetWorkItemAsync(TenantId tenantId, ProjectId projectId, WorkItemId id,
        CancellationToken ct = default);
    Task<IReadOnlyList<WorkItem>> ListWorkItemsAsync(TenantId tenantId, ProjectId projectId,
        CancellationToken ct = default);
    Task<bool> WorkItemKeyExistsAsync(TenantId tenantId, ProjectId projectId, string key,
        CancellationToken ct = default);

    Task AddMilestoneAsync(Milestone milestone, CancellationToken ct = default);
    Task<Milestone?> GetMilestoneAsync(TenantId tenantId, ProjectId projectId, MilestoneId id,
        CancellationToken ct = default);
    Task<IReadOnlyList<Milestone>> ListMilestonesAsync(TenantId tenantId, ProjectId projectId,
        CancellationToken ct = default);
    Task<bool> MilestoneKeyExistsAsync(TenantId tenantId, ProjectId projectId, string key,
        CancellationToken ct = default);
    Task AddMilestoneTransitionAsync(MilestoneTransition transition, CancellationToken ct = default);
    Task<IReadOnlyList<MilestoneTransition>> ListMilestoneTransitionsAsync(TenantId tenantId,
        ProjectId projectId, MilestoneId milestoneId, CancellationToken ct = default);

    Task AddDependencyAsync(WorkItemDependency dependency, CancellationToken ct = default);
    Task<IReadOnlyList<WorkItemDependency>> ListDependenciesAsync(TenantId tenantId,
        ProjectId projectId, CancellationToken ct = default);
    Task AddTransitionsAsync(IReadOnlyList<WorkItemTransition> transitions,
        CancellationToken ct = default);
    Task<IReadOnlyList<WorkItemTransition>> ListTransitionsAsync(TenantId tenantId,
        ProjectId projectId, WorkItemId itemId, CancellationToken ct = default);
}
