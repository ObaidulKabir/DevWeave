using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Lifecycle;

public sealed class WorkItemTransition
{
    public WorkItemTransitionId Id { get; }
    public TenantId TenantId { get; }
    public ProjectId ProjectId { get; }
    public WorkItemId WorkItemId { get; }
    public WorkItemTransitionDimension Dimension { get; }
    public string FromState { get; }
    public string ToState { get; }
    public PrincipalId ActorId { get; }
    public string Reason { get; }
    public DateTimeOffset RecordedAt { get; }
    public int Version { get; }

    private WorkItemTransition()
    {
        Id = null!;
        TenantId = null!;
        ProjectId = null!;
        WorkItemId = null!;
        ActorId = null!;
        FromState = null!;
        ToState = null!;
        Reason = null!;
    }

    private WorkItemTransition(WorkItemId workItemId, TenantId tenantId, ProjectId projectId,
        WorkItemTransitionDimension dimension, string fromState, string toState,
        PrincipalId actorId, string reason, int version)
    {
        Id = WorkItemTransitionId.New();
        WorkItemId = workItemId;
        TenantId = tenantId;
        ProjectId = projectId;
        Dimension = dimension;
        FromState = fromState;
        ToState = toState;
        ActorId = actorId;
        Reason = reason;
        RecordedAt = DateTimeOffset.UtcNow;
        Version = version;
    }

    internal static WorkItemTransition Create(WorkItem item, WorkItemTransitionDimension dimension,
        string fromState, string toState, PrincipalId actorId, string reason) =>
        new(item.Id, item.TenantId, item.ProjectId, dimension, fromState, toState,
            actorId, reason, item.Version);
}
