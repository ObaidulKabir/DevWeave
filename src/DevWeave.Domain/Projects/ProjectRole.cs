namespace DevWeave.Domain.Projects;

/// <summary>
/// The project-scoped role granted through a <see cref="ProjectMembership"/> (SRS PRJ-003/TEN-005).
/// Distinct from the tenant-wide <see cref="Identity.MembershipRole"/>.
/// </summary>
public enum ProjectRole
{
    Owner = 1,
    Admin = 2,
    Member = 3,
    Viewer = 4,
}