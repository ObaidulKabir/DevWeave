using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Lifecycle;

/// <summary>
/// A project work item whose planned, reported, verified and approved states remain independent
/// (SRS LFC-003..006). Every state change returns an immutable transition for same-transaction storage.
/// </summary>
public sealed class WorkItem
{
    public WorkItemId Id { get; }
    public TenantId TenantId { get; }
    public ProjectId ProjectId { get; }
    public string Key { get; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public MilestoneId? MilestoneId { get; private set; }
    public PrincipalId? OwnerId { get; private set; }
    public DateTimeOffset? DueAt { get; private set; }
    public WorkPriority Priority { get; private set; }
    public PlannedWorkState PlannedState { get; private set; }
    public ReportedExecutionState ReportedState { get; private set; }
    public WorkVerificationState VerificationState { get; private set; }
    public WorkApprovalState ApprovalState { get; private set; }
    public PrincipalId? ReportedBy { get; private set; }
    public PrincipalId? VerifiedBy { get; private set; }
    public PrincipalId? ApprovedBy { get; private set; }
    public int Version { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public PrincipalId CreatedBy { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private WorkItem()
    {
        Id = null!;
        TenantId = null!;
        ProjectId = null!;
        Key = null!;
        Title = null!;
        Description = null!;
        CreatedBy = null!;
    }

    private WorkItem(WorkItemId id, TenantId tenantId, ProjectId projectId, string key,
        string title, string description, MilestoneId? milestoneId, PrincipalId? ownerId,
        DateTimeOffset? dueAt, WorkPriority priority, PrincipalId createdBy, DateTimeOffset now)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        Key = key;
        Title = title;
        Description = description;
        MilestoneId = milestoneId;
        OwnerId = ownerId;
        DueAt = dueAt;
        Priority = priority;
        PlannedState = PlannedWorkState.Planned;
        ReportedState = ReportedExecutionState.NotStarted;
        VerificationState = WorkVerificationState.Unverified;
        ApprovalState = WorkApprovalState.Pending;
        Version = 1;
        CreatedAt = now;
        CreatedBy = createdBy;
        UpdatedAt = now;
    }

    public static WorkItem Create(TenantId tenantId, ProjectId projectId, string key, string title,
        string description, MilestoneId? milestoneId, PrincipalId? ownerId, DateTimeOffset? dueAt,
        WorkPriority priority, PrincipalId createdBy)
    {
        RequireText(key, nameof(key), 100);
        RequireText(title, nameof(title), 200);
        ArgumentNullException.ThrowIfNull(description);
        if (description.Length > 4000)
            throw new ArgumentException("Description cannot exceed 4000 characters.", nameof(description));
        if (!Enum.IsDefined(priority))
            throw new ArgumentOutOfRangeException(nameof(priority));

        return new WorkItem(WorkItemId.New(), tenantId, projectId, key, title, description,
            milestoneId, ownerId, dueAt, priority, createdBy, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Re-plans the item's details. Execution, verification and approval are untouched: changing what is
    /// planned does not by itself invalidate reported work. Returns null when nothing changed.
    /// </summary>
    public WorkItemTransition? Replan(string title, string description, MilestoneId? milestoneId,
        PrincipalId? ownerId, DateTimeOffset? dueAt, WorkPriority priority, PrincipalId actor,
        string reason, int expectedVersion)
    {
        EnsureVersion(expectedVersion);
        EnsureReason(reason);
        RequireText(title, nameof(title), 200);
        ArgumentNullException.ThrowIfNull(description);
        if (description.Length > 4000)
            throw new ArgumentException("Description cannot exceed 4000 characters.", nameof(description));
        if (!Enum.IsDefined(priority))
            throw new ArgumentOutOfRangeException(nameof(priority));

        if (Title == title && Description == description && MilestoneId == milestoneId &&
            OwnerId == ownerId && DueAt == dueAt && Priority == priority)
            return null;

        Title = title;
        Description = description;
        MilestoneId = milestoneId;
        OwnerId = ownerId;
        DueAt = dueAt;
        Priority = priority;
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
        return WorkItemTransition.Create(this, WorkItemTransitionDimension.Details, "details", "replanned", actor, reason);
    }

    public WorkItemTransition ChangePlannedState(PlannedWorkState target, PrincipalId actor,
        string reason, int expectedVersion)
    {
        EnsureVersion(expectedVersion);
        EnsureReason(reason);
        if (!Enum.IsDefined(target) || !IsAllowed(PlannedState, target))
            throw new InvalidOperationException($"Planned state cannot transition from '{PlannedState}' to '{target}'.");

        var previous = PlannedState;
        PlannedState = target;
        return Changed(WorkItemTransitionDimension.Planned, previous, target, actor, reason);
    }

    public IReadOnlyList<WorkItemTransition> Report(ReportedExecutionState target, PrincipalId actor,
        string reason, int expectedVersion)
    {
        EnsureVersion(expectedVersion);
        EnsureReason(reason);
        if (!Enum.IsDefined(target) || !IsAllowed(ReportedState, target))
            throw new InvalidOperationException($"Reported state cannot transition from '{ReportedState}' to '{target}'.");

        var transitions = new List<WorkItemTransition>();
        var previous = ReportedState;
        ReportedState = target;
        ReportedBy = actor;
        transitions.Add(Changed(WorkItemTransitionDimension.Reported, previous, target, actor, reason));

        if (target != ReportedExecutionState.Completed)
        {
            if (VerificationState == WorkVerificationState.Verified)
            {
                var oldVerification = VerificationState;
                VerificationState = WorkVerificationState.Unverified;
                VerifiedBy = null;
                transitions.Add(Changed(WorkItemTransitionDimension.Verification,
                    oldVerification, VerificationState, actor, "Execution report changed; verification invalidated."));
            }
            // Reworking the item reopens approval, whether it had been approved or rejected; otherwise a
            // rejected item could never be approved again.
            if (ApprovalState is WorkApprovalState.Approved or WorkApprovalState.Rejected)
            {
                var oldApproval = ApprovalState;
                ApprovalState = WorkApprovalState.Pending;
                ApprovedBy = null;
                transitions.Add(Changed(WorkItemTransitionDimension.Approval,
                    oldApproval, ApprovalState, actor,
                    oldApproval == WorkApprovalState.Rejected
                        ? "Work reopened after rejection; approval is pending again."
                        : "Execution report changed; approval invalidated."));
            }
        }

        return transitions;
    }

    public WorkItemTransition Verify(PrincipalId actor, string reason, int expectedVersion)
    {
        EnsureVersion(expectedVersion);
        EnsureReason(reason);
        if (ReportedState != ReportedExecutionState.Completed)
            throw new InvalidOperationException("Work item must be reported completed before verification.");
        if (VerificationState == WorkVerificationState.Verified)
            throw new InvalidOperationException("Work item is already verified.");
        if (actor == ReportedBy)
            throw new InvalidOperationException("Whoever reported the work as completed cannot also verify it.");

        var previous = VerificationState;
        VerificationState = WorkVerificationState.Verified;
        VerifiedBy = actor;
        return Changed(WorkItemTransitionDimension.Verification, previous, VerificationState, actor, reason);
    }

    public WorkItemTransition Approve(PrincipalId actor, string reason, int expectedVersion)
    {
        EnsureVersion(expectedVersion);
        EnsureReason(reason);
        if (VerificationState != WorkVerificationState.Verified)
            throw new InvalidOperationException("Work item must be verified before approval.");
        if (actor == VerifiedBy || actor == ReportedBy)
            throw new InvalidOperationException("The reporter and verifier cannot approve the same work item.");
        if (ApprovalState != WorkApprovalState.Pending)
            throw new InvalidOperationException($"Work item approval cannot transition from '{ApprovalState}' to Approved.");

        var previous = ApprovalState;
        ApprovalState = WorkApprovalState.Approved;
        ApprovedBy = actor;
        return Changed(WorkItemTransitionDimension.Approval, previous, ApprovalState, actor, reason);
    }

    public WorkItemTransition RejectApproval(PrincipalId actor, string reason, int expectedVersion)
    {
        EnsureVersion(expectedVersion);
        EnsureReason(reason);
        if (VerificationState != WorkVerificationState.Verified)
            throw new InvalidOperationException("Work item must be verified before approval can be rejected.");
        if (ApprovalState != WorkApprovalState.Pending)
            throw new InvalidOperationException($"Work item approval cannot transition from '{ApprovalState}' to Rejected.");

        var previous = ApprovalState;
        ApprovalState = WorkApprovalState.Rejected;
        ApprovedBy = actor;
        return Changed(WorkItemTransitionDimension.Approval, previous, ApprovalState, actor, reason);
    }

    private WorkItemTransition Changed<T>(WorkItemTransitionDimension dimension, T from, T to,
        PrincipalId actor, string reason) where T : struct, Enum
    {
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
        return WorkItemTransition.Create(this, dimension, from.ToString(), to.ToString(), actor, reason);
    }

    private void EnsureVersion(int expectedVersion)
    {
        if (expectedVersion != Version)
            throw new WorkItemConcurrencyException(expectedVersion, Version);
    }

    private static bool IsAllowed(PlannedWorkState from, PlannedWorkState to) =>
        (from, to) switch
        {
            (PlannedWorkState.Planned, PlannedWorkState.Ready or PlannedWorkState.Cancelled) => true,
            (PlannedWorkState.Ready, PlannedWorkState.Planned or PlannedWorkState.InProgress or PlannedWorkState.Cancelled) => true,
            // Re-planning: cancelled work can be brought back into the plan.
            (PlannedWorkState.Cancelled, PlannedWorkState.Planned) => true,
            (PlannedWorkState.InProgress, PlannedWorkState.Blocked or PlannedWorkState.Cancelled) => true,
            (PlannedWorkState.Blocked, PlannedWorkState.InProgress or PlannedWorkState.Cancelled) => true,
            _ => false,
        };

    private static bool IsAllowed(ReportedExecutionState from, ReportedExecutionState to) =>
        (from, to) switch
        {
            (ReportedExecutionState.NotStarted, ReportedExecutionState.InProgress or ReportedExecutionState.Blocked or ReportedExecutionState.Completed or ReportedExecutionState.Failed) => true,
            (ReportedExecutionState.InProgress, ReportedExecutionState.Blocked or ReportedExecutionState.Completed or ReportedExecutionState.Failed) => true,
            (ReportedExecutionState.Blocked, ReportedExecutionState.InProgress or ReportedExecutionState.Completed or ReportedExecutionState.Failed) => true,
            (ReportedExecutionState.Completed, ReportedExecutionState.InProgress) => true,
            (ReportedExecutionState.Failed, ReportedExecutionState.InProgress) => true,
            _ => false,
        };

    private static void EnsureReason(string reason) => RequireText(reason, nameof(reason), 1000);

    private static void RequireText(string value, string parameter, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{parameter} is required.", parameter);
        if (value.Length > maxLength)
            throw new ArgumentException($"{parameter} cannot exceed {maxLength} characters.", parameter);
    }
}
