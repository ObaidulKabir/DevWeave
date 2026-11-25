using DevWeave.Domain.Identity;

namespace DevWeave.Domain.Commercial;

/// <summary>
/// Tenant content stays out of cross-customer training unless a person records an explicit opt-in (SRS COM-025).
/// </summary>
public sealed class TenantTrainingPolicy
{
    public TenantId TenantId { get; }
    public bool OptedIn { get; private set; }
    public PrincipalId? ActorId { get; private set; }
    public DateTimeOffset? ChangedAt { get; private set; }
    public string? Reason { get; private set; }

    private TenantTrainingPolicy(
        TenantId tenantId,
        bool optedIn,
        PrincipalId? actorId,
        DateTimeOffset? changedAt,
        string? reason)
    {
        TenantId = tenantId;
        OptedIn = optedIn;
        ActorId = actorId;
        ChangedAt = changedAt;
        Reason = reason;
    }

    public static TenantTrainingPolicy Excluded(TenantId tenantId) =>
        new(tenantId, false, null, null, null);

    public void OptIn(PrincipalId actor, string reason, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("An opt-in reason is required.", nameof(reason));
        OptedIn = true;
        ActorId = actor;
        Reason = reason.Trim();
        ChangedAt = at;
    }

    public string Use => OptedIn ? "OptedIn" : "Excluded";
}
