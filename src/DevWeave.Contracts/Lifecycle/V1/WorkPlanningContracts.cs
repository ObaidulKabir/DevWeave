using DevWeave.Domain.Lifecycle;

namespace DevWeave.Contracts.Lifecycle.V1;

public sealed record CreateMilestoneRequest(
    string Key,
    string Title,
    DateTimeOffset? DueAt);

public sealed record MilestoneResponse(
    Guid Id,
    Guid TenantId,
    Guid ProjectId,
    string Key,
    string Title,
    DateTimeOffset? DueAt,
    MilestoneState State,
    int Version,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record TransitionMilestoneRequest(
    MilestoneState State,
    string Reason,
    int ExpectedVersion);

public sealed record MilestoneTransitionResponse(
    Guid Id,
    Guid MilestoneId,
    MilestoneState FromState,
    MilestoneState ToState,
    Guid ActorId,
    string Reason,
    int Version,
    DateTimeOffset RecordedAt);

public sealed record CreateWorkItemRequest(
    string Key,
    string Title,
    string Description,
    Guid? MilestoneId,
    Guid? OwnerId,
    DateTimeOffset? DueAt,
    WorkPriority Priority,
    IReadOnlyList<Guid> DependsOn);

public sealed record WorkItemResponse(
    Guid Id,
    Guid TenantId,
    Guid ProjectId,
    string Key,
    string Title,
    string Description,
    Guid? MilestoneId,
    Guid? OwnerId,
    DateTimeOffset? DueAt,
    WorkPriority Priority,
    PlannedWorkState PlannedState,
    ReportedExecutionState ReportedState,
    WorkVerificationState VerificationState,
    WorkApprovalState ApprovalState,
    int Version,
    IReadOnlyList<Guid> DependsOn,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record UpdatePlannedStateRequest(
    PlannedWorkState State,
    string Reason,
    int ExpectedVersion);

public sealed record ReportWorkRequest(
    ReportedExecutionState State,
    string Reason,
    int ExpectedVersion);

public sealed record ReviewWorkRequest(
    string Reason,
    int ExpectedVersion);

public sealed record WorkItemTransitionResponse(
    Guid Id,
    Guid WorkItemId,
    WorkItemTransitionDimension Dimension,
    string FromState,
    string ToState,
    Guid ActorId,
    string Reason,
    DateTimeOffset RecordedAt,
    int Version);
