namespace DevWeave.Domain.Commercial;

public record CommercialPlanId(Guid Value)
{
    public static CommercialPlanId New() => new(Guid.NewGuid());
}

public record EntitlementRuleId(Guid Value)
{
    public static EntitlementRuleId New() => new(Guid.NewGuid());
}

public record UsageObservationId(Guid Value)
{
    public static UsageObservationId New() => new(Guid.NewGuid());
}

public record TenantPlanAssignmentId(Guid Value)
{
    public static TenantPlanAssignmentId New() => new(Guid.NewGuid());
}
