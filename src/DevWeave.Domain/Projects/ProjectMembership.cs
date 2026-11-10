using DevWeave.Domain.Identity;

namespace DevWeave.Domain.Projects;

/// <summary>
/// Binds a <see cref="Principal"/> to a <see cref="Project"/> with a project-scoped role
/// (SRS PRJ-003/TEN-005). The member's role is independent of the tenant-wide membership role.
/// </summary>
public sealed class ProjectMembership
{
    public ProjectMembershipId Id { get; }
    public TenantId TenantId { get; }
    public ProjectId ProjectId { get; }
    public PrincipalId PrincipalId { get; }
    public ProjectRole Role { get; private set; }
    public ProjectMembershipStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? RevokedAt { get; private set; }

    private ProjectMembership(
        ProjectMembershipId id,
        TenantId tenantId,
        ProjectId projectId,
        PrincipalId principalId,
        ProjectRole role,
        ProjectMembershipStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset? revokedAt)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        PrincipalId = principalId;
        Role = role;
        Status = status;
        CreatedAt = createdAt;
        RevokedAt = revokedAt;
    }

    public static ProjectMembership Grant(TenantId tenantId, ProjectId projectId, PrincipalId principalId, ProjectRole role)
    {
        return new ProjectMembership(
            ProjectMembershipId.New(),
            tenantId,
            projectId,
            principalId,
            role,
            ProjectMembershipStatus.Active,
            DateTimeOffset.UtcNow,
            revokedAt: null);
    }

    public void Revoke()
    {
        if (Status == ProjectMembershipStatus.Revoked)
            throw new InvalidOperationException("Project membership is already revoked.");

        Status = ProjectMembershipStatus.Revoked;
        RevokedAt = DateTimeOffset.UtcNow;
    }
}