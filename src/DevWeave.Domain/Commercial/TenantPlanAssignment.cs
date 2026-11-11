using DevWeave.Domain.Identity;

namespace DevWeave.Domain.Commercial;

/// <summary>
/// Assigns a commercial plan to a tenant for a specific period (SRS COM-001).
/// </summary>
public sealed class TenantPlanAssignment
{
    public TenantPlanAssignmentId Id { get; }
    public TenantId TenantId { get; }
    public CommercialPlanId PlanId { get; }
    public DateTimeOffset EffectiveFrom { get; }
    public DateTimeOffset? EffectiveTo { get; }
    public bool IsActive { get; private set; }

    private TenantPlanAssignment(
        TenantPlanAssignmentId id,
        TenantId tenantId,
        CommercialPlanId planId,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo,
        bool isActive)
    {
        Id = id;
        TenantId = tenantId;
        PlanId = planId;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        IsActive = isActive;
    }

    public static TenantPlanAssignment Assign(
        TenantId tenantId,
        CommercialPlanId planId,
        DateTimeOffset effectiveFrom)
    {
        return new TenantPlanAssignment(
            TenantPlanAssignmentId.New(),
            tenantId,
            planId,
            effectiveFrom,
            null,
            true);
    }

    public void Deactivate(DateTimeOffset at)
    {
        IsActive = false;
        // In a real scenario, we might set EffectiveTo = at
    }
}
