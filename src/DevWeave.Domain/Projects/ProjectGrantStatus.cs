namespace DevWeave.Domain.Projects;

/// <summary>
/// Lifecycle of a scoped project grant (SRS PRJ-003/TEN-005). A grant is issued Granted and can be
/// retracted to Revoked, which is terminal.
/// </summary>
public enum ProjectGrantStatus
{
    Granted = 1,
    Revoked = 2,
}