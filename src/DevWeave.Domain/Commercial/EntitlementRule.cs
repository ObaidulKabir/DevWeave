namespace DevWeave.Domain.Commercial;

public enum EntitlementAction
{
    Allow,
    Block,
    Warn
}

/// <summary>
/// A rule defining a limit and unit for a specific entitlement within a plan (SRS COM-002).
/// </summary>
public sealed class EntitlementRule
{
    public EntitlementRuleId Id { get; }
    public CommercialPlanId PlanId { get; }
    public string Key { get; }
    public decimal Limit { get; }
    public string Unit { get; }
    public decimal OverageRate { get; }
    public EntitlementAction Action { get; }

    private EntitlementRule(
        EntitlementRuleId id,
        CommercialPlanId planId,
        string key,
        decimal limit,
        string unit,
        decimal overageRate,
        EntitlementAction action)
    {
        Id = id;
        PlanId = planId;
        Key = key;
        Limit = limit;
        Unit = unit;
        OverageRate = overageRate;
        Action = action;
    }

    public static EntitlementRule Create(
        CommercialPlanId planId,
        string key,
        decimal limit,
        string unit,
        EntitlementAction action,
        decimal overageRate = 0)
    {
        return new EntitlementRule(
            EntitlementRuleId.New(),
            planId,
            key,
            limit,
            unit,
            overageRate,
            action);
    }
}
