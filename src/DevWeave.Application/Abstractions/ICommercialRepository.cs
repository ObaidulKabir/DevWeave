using DevWeave.Domain.Commercial;
using DevWeave.Domain.Identity;

namespace DevWeave.Application.Abstractions;

public interface ICommercialRepository
{
    // Plans
    Task AddPlanAsync(CommercialPlan plan, CancellationToken ct = default);
    Task<CommercialPlan?> GetPlanByIdAsync(CommercialPlanId planId, CancellationToken ct = default);
    Task<CommercialPlan?> GetPlanByKeyAsync(string key, string version, CancellationToken ct = default);
    
    // Entitlements
    Task AddEntitlementRuleAsync(EntitlementRule rule, CancellationToken ct = default);
    Task<IReadOnlyList<EntitlementRule>> GetRulesByPlanIdAsync(CommercialPlanId planId, CancellationToken ct = default);
    
    // Assignments
    Task AddAssignmentAsync(TenantPlanAssignment assignment, CancellationToken ct = default);
    Task<TenantPlanAssignment?> GetActiveAssignmentAsync(TenantId tenantId, DateTimeOffset at, CancellationToken ct = default);
    
    // Usage
    Task AddUsageObservationAsync(UsageObservation observation, CancellationToken ct = default);
    Task<decimal> GetUsageTotalAsync(TenantId tenantId, string meter, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
    Task<IReadOnlyList<UsageObservation>> ListUsageAsync(TenantId tenantId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
    Task<UsageObservation?> GetUsageByIdempotencyKeyAsync(TenantId tenantId, string idempotencyKey, CancellationToken ct = default);
}
