namespace DevWeave.Domain.Projects;

/// <summary>
/// The scope of a fine-grained project grant (SRS PRJ-003/TEN-005). A grant is narrower than a role and
/// attaches to a specific capability rather than a whole membership.
/// </summary>
public enum ProjectGrantScope
{
    ManageMembers = 1,
    ManageReferences = 2,
    Deploy = 3,
}