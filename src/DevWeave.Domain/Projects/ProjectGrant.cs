using DevWeave.Domain.Identity;

namespace DevWeave.Domain.Projects;

/// <summary>
/// A fine-grained, scoped grant of a single capability to a principal within a project
/// (SRS PRJ-003/TEN-005). Grants are narrower than a role-based membership and can be retracted.
/// </summary>
public sealed class ProjectGrant
{
    public ProjectGrantId Id { get; }
    public TenantId TenantId { get; }
    public ProjectId ProjectId { get; }
    public PrincipalId PrincipalId { get; }
    public ProjectGrantScope Scope { get; }
    public ProjectGrantStatus Status { get; private set; }
    public DateTimeOffset GrantedAt { get; }
    public DateTimeOffset? RevokedAt { get; private set; }

    private ProjectGrant(
        ProjectGrantId id,
        TenantId tenantId,
        ProjectId projectId,
        PrincipalId principalId,
        ProjectGrantScope scope,
        ProjectGrantStatus status,
        DateTimeOffset grantedAt,
        DateTimeOffset? revokedAt)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        PrincipalId = principalId;
        Scope = scope;
        Status = status;
        GrantedAt = grantedAt;
        RevokedAt = revokedAt;
    }

    public static ProjectGrant Grant(TenantId tenantId, ProjectId projectId, PrincipalId principalId, ProjectGrantScope scope)
    {
        return new ProjectGrant(
            ProjectGrantId.New(),
            tenantId,
            projectId,
            principalId,
            scope,
            ProjectGrantStatus.Granted,
            DateTimeOffset.UtcNow,
            revokedAt: null);
    }

    public void Revoke()
    {
        if (Status == ProjectGrantStatus.Revoked)
            throw new InvalidOperationException("Project grant is already revoked.");

        Status = ProjectGrantStatus.Revoked;
        RevokedAt = DateTimeOffset.UtcNow;
    }
}