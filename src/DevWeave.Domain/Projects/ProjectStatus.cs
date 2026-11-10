namespace DevWeave.Domain.Projects;

/// <summary>
/// Lifecycle of a project (SRS PRJ-004). Draft is the creation state; Active/OnHold/Completed are
/// governed working states; Archived is a controlled, reversible resting state preceding deletion.
/// </summary>
public enum ProjectStatus
{
    Draft = 1,
    Active = 2,
    OnHold = 3,
    Completed = 4,
    Archived = 5,
}