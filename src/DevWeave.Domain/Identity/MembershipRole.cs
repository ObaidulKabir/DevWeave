namespace DevWeave.Domain.Identity;

/// <summary>
/// The tenant-level role granted to a <see cref="Principal"/> through a <see cref="Membership"/> (SRS TEN-004).
/// </summary>
public enum MembershipRole
{
    Owner = 1,
    Admin = 2,
    Member = 3,
}