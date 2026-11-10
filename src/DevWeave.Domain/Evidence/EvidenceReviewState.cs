namespace DevWeave.Domain.Evidence;

/// <summary>
/// The review progression of an evidence record (SRS PRO-006). Evidence starts <see cref="Unreviewed"/>
/// and can be accepted, rejected, or contested. A claim (Slice 6 <c>Activity</c> of type
/// <see cref="DevWeave.Domain.Activities.ActivityType.Claim"/>) is only verified once it has at least
/// one <see cref="Accepted"/> evidence record.
/// </summary>
public enum EvidenceReviewState
{
    Unreviewed = 1,
    Accepted = 2,
    Rejected = 3,
    Contested = 4,
}