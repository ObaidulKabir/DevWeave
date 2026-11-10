namespace DevWeave.Domain.Identity;

/// <summary>
/// Lifecycle of a tenant membership (SRS TEN-004). Invited members become Active on enrolment;
/// Active or Invited members can be Revoked, which is the terminal state.
/// </summary>
public enum MembershipStatus
{
    Invited = 1,
    Active = 2,
    Revoked = 3,
}