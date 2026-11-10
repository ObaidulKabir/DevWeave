namespace DevWeave.Domain.Projects;

/// <summary>
/// Lifecycle of a project membership (SRS PRJ-003). A membership is granted Active and can be retracted
/// to Revoked, which is terminal.
/// </summary>
public enum ProjectMembershipStatus
{
    Active = 1,
    Revoked = 2,
}