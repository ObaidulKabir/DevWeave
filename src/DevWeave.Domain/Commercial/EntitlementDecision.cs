using DevWeave.Domain.Identity;

namespace DevWeave.Domain.Commercial;

/// <summary>
/// A stored entitlement decision so a denied launch can be traced to the plan, meter, and time (SRS COM-024).
/// </summary>
public sealed class EntitlementDecision
{
    public EntitlementDecisionId Id { get; }
    public TenantId TenantId { get; }
    public PrincipalId ActorId { get; }
    public CommercialPlanId? PlanId { get; }
    public string? PlanVersion { get; }
    public string Meter { get; }
    public decimal Requested { get; }
    public decimal Observed { get; }
    public string Outcome { get; }
    public string Reason { get; }
    public string PolicyVersion { get; }
    public DateTimeOffset DecidedAt { get; }

    private EntitlementDecision(
        EntitlementDecisionId id,
        TenantId tenantId,
        PrincipalId actorId,
        CommercialPlanId? planId,
        string? planVersion,
        string meter,
        decimal requested,
        decimal observed,
        string outcome,
        string reason,
        string policyVersion,
        DateTimeOffset decidedAt)
    {
        Id = id;
        TenantId = tenantId;
        ActorId = actorId;
        PlanId = planId;
        PlanVersion = planVersion;
        Meter = meter;
        Requested = requested;
        Observed = observed;
        Outcome = outcome;
        Reason = reason;
        PolicyVersion = policyVersion;
        DecidedAt = decidedAt;
    }

    public static EntitlementDecision Record(
        TenantId tenantId,
        PrincipalId actorId,
        CommercialPlanId? planId,
        string? planVersion,
        string meter,
        decimal requested,
        decimal observed,
        string outcome,
        string reason,
        string policyVersion,
        DateTimeOffset decidedAt)
    {
        if (string.IsNullOrWhiteSpace(meter))
            throw new ArgumentException("Meter is required.", nameof(meter));
        if (string.IsNullOrWhiteSpace(outcome))
            throw new ArgumentException("Outcome is required.", nameof(outcome));
        return new EntitlementDecision(
            EntitlementDecisionId.New(),
            tenantId,
            actorId,
            planId,
            string.IsNullOrWhiteSpace(planVersion) ? null : planVersion.Trim(),
            meter.Trim(),
            requested,
            observed,
            outcome.Trim(),
            reason.Trim(),
            policyVersion.Trim(),
            decidedAt);
    }
}
