using DevWeave.Domain.Identity;

namespace DevWeave.Domain.Commercial;

/// <summary>
/// A versioned commercial plan that defines the entitlements for a tenant (SRS COM-001).
/// </summary>
public sealed class CommercialPlan
{
    public CommercialPlanId Id { get; }
    public string Key { get; }
    public string Version { get; }
    public decimal Price { get; }
    public string Currency { get; }
    public DateTimeOffset EffectiveFrom { get; }
    public DateTimeOffset? EffectiveTo { get; }
    public bool IsActive { get; private set; }

    private CommercialPlan(
        CommercialPlanId id,
        string key,
        string version,
        decimal price,
        string currency,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo,
        bool isActive)
    {
        Id = id;
        Key = key;
        Version = version;
        Price = price;
        Currency = currency;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        IsActive = isActive;
    }

    public static CommercialPlan Create(
        string key,
        string version,
        DateTimeOffset effectiveFrom,
        decimal price = 0,
        string currency = "USD")
    {
        return new CommercialPlan(
            CommercialPlanId.New(),
            key,
            version,
            price,
            currency,
            effectiveFrom,
            null,
            true);
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
