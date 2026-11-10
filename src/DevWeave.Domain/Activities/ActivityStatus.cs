namespace DevWeave.Domain.Activities;

/// <summary>
/// The lifecycle state of a recorded <see cref="Activity"/> (SRS PRO-002). Activities are append-only
/// (REV-006): the status is captured at recording time and never mutated afterwards.
/// </summary>
public enum ActivityStatus
{
    Recorded = 1,
    InProgress = 2,
    Completed = 3,
    Failed = 4,
}