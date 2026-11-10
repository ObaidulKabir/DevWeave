using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Lifecycle;

public sealed class WorkItemDependency
{
    public WorkItemDependencyId Id { get; }
    public TenantId TenantId { get; }
    public ProjectId ProjectId { get; }
    public WorkItemId WorkItemId { get; }
    public WorkItemId DependsOnId { get; }
    public PrincipalId CreatedBy { get; }
    public DateTimeOffset CreatedAt { get; }

    private WorkItemDependency()
    {
        Id = null!;
        TenantId = null!;
        ProjectId = null!;
        WorkItemId = null!;
        DependsOnId = null!;
        CreatedBy = null!;
    }

    private WorkItemDependency(TenantId tenantId, ProjectId projectId, WorkItemId workItemId,
        WorkItemId dependsOnId, PrincipalId createdBy)
    {
        Id = WorkItemDependencyId.New();
        TenantId = tenantId;
        ProjectId = projectId;
        WorkItemId = workItemId;
        DependsOnId = dependsOnId;
        CreatedBy = createdBy;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public static WorkItemDependency Create(TenantId tenantId, ProjectId projectId,
        WorkItemId workItemId, WorkItemId dependsOnId, PrincipalId createdBy)
    {
        if (workItemId == dependsOnId)
            throw new ArgumentException("A work item cannot depend on itself.", nameof(dependsOnId));
        return new WorkItemDependency(tenantId, projectId, workItemId, dependsOnId, createdBy);
    }
}
