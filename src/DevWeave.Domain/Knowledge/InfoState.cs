namespace DevWeave.Domain.Knowledge;

/// <summary>
/// The information lifecycle state of a knowledge item (SRS KNW-011, BR-009). Generated or
/// agent-created content is always stored as Proposed; Approved/Rejected/Verified can only be
/// reached through explicit actions, never by simply persisting the item.
/// </summary>
public enum InfoState
{
    Proposed = 1,
    Approved = 2,
    Rejected = 3,
    Verified = 4,
}