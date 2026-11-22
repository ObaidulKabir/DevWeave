namespace DevWeave.Domain.Identity;

/// <summary>
/// Time-bounded, audited platform support access to a tenant (SRS TEN-009).
/// No standing support access exists without an active grant.
/// </summary>
public sealed class SupportAccessGrant
{
    public SupportAccessGrantId Id { get; }
    public TenantId TenantId { get; }
    public PrincipalId OperatorId { get; }
    public PrincipalId ApproverId { get; }
    public string Purpose { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset ExpiresAt { get; }
    public DateTimeOffset? RevokedAt { get; private set; }

    private SupportAccessGrant(
        SupportAccessGrantId id,
        TenantId tenantId,
        PrincipalId operatorId,
        PrincipalId approverId,
        string purpose,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        DateTimeOffset? revokedAt)
    {
        Id = id;
        TenantId = tenantId;
        OperatorId = operatorId;
        ApproverId = approverId;
        Purpose = purpose;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        RevokedAt = revokedAt;
    }

    public bool IsActive(DateTimeOffset now) =>
        RevokedAt is null && ExpiresAt > now;

    public static SupportAccessGrant Issue(
        TenantId tenantId,
        PrincipalId operatorId,
        PrincipalId approverId,
        string purpose,
        TimeSpan lifetime,
        DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(purpose))
            throw new ArgumentException("Support access purpose is required.", nameof(purpose));
        if (lifetime <= TimeSpan.Zero || lifetime > TimeSpan.FromDays(30))
            throw new ArgumentOutOfRangeException(nameof(lifetime), "Support access lifetime must be greater than zero and at most 30 days.");

        var created = now ?? DateTimeOffset.UtcNow;
        return new SupportAccessGrant(
            SupportAccessGrantId.New(),
            tenantId,
            operatorId,
            approverId,
            purpose.Trim(),
            created,
            created.Add(lifetime),
            revokedAt: null);
    }

    public void Revoke(DateTimeOffset? now = null)
    {
        if (RevokedAt is not null)
            throw new InvalidOperationException("Support access grant is already revoked.");

        RevokedAt = now ?? DateTimeOffset.UtcNow;
    }
}

public readonly record struct SupportAccessGrantId(Guid Value)
{
    public static SupportAccessGrantId New() => new(Guid.NewGuid());
}
