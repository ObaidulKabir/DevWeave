using System.Text.Json;
using DevWeave.Application.Abstractions;
using DevWeave.Domain.Audit;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Lifecycle;

/// <summary>
/// Re-planning of delivery work. A project Owner (or Admin, who acts with owner rights) can always change
/// the plan, whatever state implementation is in: edit work items and milestones and add or remove
/// dependencies. Every change is versioned (optimistic concurrency) and audited with who, what and why.
/// </summary>
public sealed class ReplanWork(
    IProjectMembershipRepository memberships,
    IPrincipalRepository principals,
    IWorkPlanningRepository work,
    IAuditRepository audit,
    IUnitOfWork unitOfWork)
{
    public async Task<WorkItem> ReplanWorkItemAsync(TenantId tenantId, ProjectId projectId, WorkItemId itemId,
        string title, string description, MilestoneId? milestoneId, PrincipalId? ownerId, DateTimeOffset? dueAt,
        WorkPriority priority, PrincipalId actorId, string reason, int expectedVersion, CancellationToken ct = default)
    {
        await EnsureOwnerAsync(tenantId, projectId, actorId, ct);
        var item = await work.GetWorkItemAsync(tenantId, projectId, itemId, ct)
            ?? throw new NotFoundException($"Work item '{itemId}' was not found within this project.");

        if (milestoneId is not null && await work.GetMilestoneAsync(tenantId, projectId, milestoneId, ct) is null)
            throw new NotFoundException($"Milestone '{milestoneId}' was not found within this project.");
        if (ownerId is not null)
        {
            var owner = await principals.GetByIdAsync(tenantId, ownerId, ct);
            if (owner is null || !owner.IsActive)
                throw new NotFoundException($"Owner '{ownerId}' was not found within tenant '{tenantId}'.");
            await ProjectWorkAuthorization.EnsureMemberAsync(memberships, tenantId, projectId, ownerId, reviewerOnly: false, ct);
        }

        var before = Snapshot(item);
        var transition = item.Replan(title, description, milestoneId, ownerId, dueAt, priority, actorId, reason, expectedVersion);
        if (transition is null)
            return item;

        await work.AddTransitionsAsync([transition], ct);
        await RecordAsync(tenantId, actorId, "work.item.replanned", "WorkItem", item.Id.Value.ToString(),
            new { projectId = projectId.Value, reason, before, after = Snapshot(item) }, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return item;
    }

    public async Task<Milestone> ReplanMilestoneAsync(TenantId tenantId, ProjectId projectId, MilestoneId milestoneId,
        string title, DateTimeOffset? dueAt, PrincipalId actorId, string reason, int expectedVersion,
        CancellationToken ct = default)
    {
        await EnsureOwnerAsync(tenantId, projectId, actorId, ct);
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
            throw new ArgumentException("A reason of 1-1000 characters is required.", nameof(reason));

        var milestone = await work.GetMilestoneAsync(tenantId, projectId, milestoneId, ct)
            ?? throw new NotFoundException($"Milestone '{milestoneId}' was not found within this project.");
        var before = new { milestone.Title, milestone.DueAt };
        if (!milestone.Replan(title, dueAt, expectedVersion))
            return milestone;

        await RecordAsync(tenantId, actorId, "milestone.replanned", "Milestone", milestone.Id.Value.ToString(),
            new { projectId = projectId.Value, reason, before, after = new { milestone.Title, milestone.DueAt } }, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return milestone;
    }

    public async Task<WorkItemDependency> AddDependencyAsync(TenantId tenantId, ProjectId projectId,
        WorkItemId itemId, WorkItemId dependsOnId, PrincipalId actorId, CancellationToken ct = default)
    {
        await EnsureOwnerAsync(tenantId, projectId, actorId, ct);
        _ = await work.GetWorkItemAsync(tenantId, projectId, itemId, ct)
            ?? throw new NotFoundException($"Work item '{itemId}' was not found within this project.");
        _ = await work.GetWorkItemAsync(tenantId, projectId, dependsOnId, ct)
            ?? throw new NotFoundException($"Dependency '{dependsOnId}' was not found within this project.");

        var existing = await work.ListDependenciesAsync(tenantId, projectId, ct);
        if (existing.Any(d => d.WorkItemId == itemId && d.DependsOnId == dependsOnId))
            throw new ConflictException("This dependency already exists.");
        if (Reaches(existing, dependsOnId, itemId))
            throw new ConflictException("This dependency would create a cycle: the other item already depends on this one.");

        var dependency = WorkItemDependency.Create(tenantId, projectId, itemId, dependsOnId, actorId);
        await work.AddDependencyAsync(dependency, ct);
        await RecordAsync(tenantId, actorId, "work.dependency.added", "WorkItem", itemId.Value.ToString(),
            new { projectId = projectId.Value, dependsOnId = dependsOnId.Value }, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return dependency;
    }

    public async Task RemoveDependencyAsync(TenantId tenantId, ProjectId projectId,
        WorkItemId itemId, WorkItemId dependsOnId, PrincipalId actorId, CancellationToken ct = default)
    {
        await EnsureOwnerAsync(tenantId, projectId, actorId, ct);
        var dependency = (await work.ListDependenciesAsync(tenantId, projectId, ct))
            .FirstOrDefault(d => d.WorkItemId == itemId && d.DependsOnId == dependsOnId)
            ?? throw new NotFoundException("This dependency does not exist.");

        await work.RemoveDependencyAsync(dependency, ct);
        await RecordAsync(tenantId, actorId, "work.dependency.removed", "WorkItem", itemId.Value.ToString(),
            new { projectId = projectId.Value, dependsOnId = dependsOnId.Value }, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>True when <paramref name="from"/> already depends (directly or transitively) on <paramref name="to"/>.</summary>
    private static bool Reaches(IReadOnlyList<WorkItemDependency> dependencies, WorkItemId from, WorkItemId to)
    {
        var next = dependencies.ToLookup(d => d.WorkItemId, d => d.DependsOnId);
        var seen = new HashSet<WorkItemId>();
        var stack = new Stack<WorkItemId>([from]);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current == to)
                return true;
            if (!seen.Add(current))
                continue;
            foreach (var target in next[current])
                stack.Push(target);
        }

        return false;
    }

    private async Task EnsureOwnerAsync(TenantId tenantId, ProjectId projectId, PrincipalId actorId, CancellationToken ct)
    {
        var principal = await principals.GetByIdAsync(tenantId, actorId, ct);
        if (principal is null || !principal.IsActive || principal.Type != PrincipalType.Human)
            throw new NotFoundException($"Principal '{actorId}' was not found within tenant '{tenantId}'.");
        await ProjectWorkAuthorization.EnsureMemberAsync(memberships, tenantId, projectId, actorId, reviewerOnly: true, ct);
    }

    private static object Snapshot(WorkItem item) => new
    {
        item.Title,
        item.Description,
        milestoneId = item.MilestoneId?.Value,
        ownerId = item.OwnerId?.Value,
        item.DueAt,
        priority = item.Priority.ToString(),
    };

    private Task RecordAsync(TenantId tenantId, PrincipalId actorId, string action, string resourceType,
        string resourceId, object metadata, CancellationToken ct) =>
        audit.AddAsync(
            AuditRecord.Record(tenantId, actorId, action, resourceType, resourceId, JsonSerializer.Serialize(metadata)),
            ct);
}
