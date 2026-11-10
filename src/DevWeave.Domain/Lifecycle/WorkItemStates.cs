namespace DevWeave.Domain.Lifecycle;

public enum PlannedWorkState
{
    Planned = 1,
    Ready = 2,
    InProgress = 3,
    Blocked = 4,
    Cancelled = 5,
}

public enum ReportedExecutionState
{
    NotStarted = 1,
    InProgress = 2,
    Blocked = 3,
    Completed = 4,
    Failed = 5,
}

public enum WorkVerificationState
{
    Unverified = 1,
    Verified = 2,
}

public enum WorkApprovalState
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
}

public enum WorkPriority
{
    Low = 1,
    Normal = 2,
    High = 3,
    Critical = 4,
}

public enum MilestoneState
{
    Planned = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4,
}

public enum WorkItemTransitionDimension
{
    Planned = 1,
    Reported = 2,
    Verification = 3,
    Approval = 4,
}
