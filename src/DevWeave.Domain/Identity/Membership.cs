namespace DevWeave.Domain.Identity;

/// <summary>
/// Binds a <see cref="Principal"/> to a <see cref="Tenant"/> with a role and a tracked lifecycle
/// (SRS TEN-004). The owning tenant is carried directly on the row and is immutable.
/// </summary>
public sealed class Membership
{
    public MembershipId Id { get; }
    public TenantId TenantId { get; }
    public PrincipalId PrincipalId { get; }
    public MembershipRole Role { get; private set; }
    public MembershipStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? ActivatedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    private Membership(
        MembershipId id,
        TenantId tenantId,
        PrincipalId principalId,
        MembershipRole role,
        MembershipStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset? activatedAt,
        DateTimeOffset? revokedAt)
    {
        Id = id;
        TenantId = tenantId;
        PrincipalId = principalId;
        Role = role;
        Status = status;
        CreatedAt = createdAt;
        ActivatedAt = activatedAt;
        RevokedAt = revokedAt;
    }

    public static Membership Invite(TenantId tenantId, PrincipalId principalId, MembershipRole role)
    {
        return new Membership(
            MembershipId.New(),
            tenantId,
            principalId,
            role,
            MembershipStatus.Invited,
            DateTimeOffset.UtcNow,
            activatedAt: null,
            revokedAt: null);
    }

    public void Activate()
    {
        if (Status != MembershipStatus.Invited)
            throw new InvalidOperationException($"Membership cannot be activated from '{Status}' state.");

        Status = MembershipStatus.Active;
        ActivatedAt = DateTimeOffset.UtcNow;
    }

    public void Revoke()
    {
        if (Status == MembershipStatus.Revoked)
            throw new InvalidOperationException("Membership is already revoked.");

        Status = MembershipStatus.Revoked;
        RevokedAt = DateTimeOffset.UtcNow;
    }
}