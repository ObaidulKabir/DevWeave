using DevWeave.Application.Abstractions;
using DevWeave.Domain.Commercial;
using DevWeave.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace DevWeave.Infrastructure.Repositories;

public sealed class CommercialRepository : ICommercialRepository
{
    private readonly DevWeave.Infrastructure.Persistence.DevWeaveDbContext _context;

    public CommercialRepository(DevWeave.Infrastructure.Persistence.DevWeaveDbContext context)
    {
        _context = context;
    }

    // Plans
    public async Task AddPlanAsync(CommercialPlan plan, CancellationToken ct = default)
    {
        await _context.CommercialPlans.AddAsync(plan, ct);
    }

    public async Task<CommercialPlan?> GetPlanByIdAsync(CommercialPlanId planId, CancellationToken ct = default)
    {
        return await _context.CommercialPlans
            .FirstOrDefaultAsync(p => p.Id == planId, ct);
    }

    public async Task<CommercialPlan?> GetPlanByKeyAsync(string key, string version, CancellationToken ct = default)
    {
        return await _context.CommercialPlans
            .FirstOrDefaultAsync(p => p.Key == key && p.Version == version, ct);
    }

    // Entitlements
    public async Task AddEntitlementRuleAsync(EntitlementRule rule, CancellationToken ct = default)
    {
        await _context.EntitlementRules.AddAsync(rule, ct);
    }

    public async Task<IReadOnlyList<EntitlementRule>> GetRulesByPlanIdAsync(CommercialPlanId planId, CancellationToken ct = default)
    {
        return await _context.EntitlementRules
            .Where(r => r.PlanId == planId)
            .ToListAsync(ct);
    }

    // Assignments
    public async Task AddAssignmentAsync(TenantPlanAssignment assignment, CancellationToken ct = default)
    {
        await _context.TenantPlanAssignments.AddAsync(assignment, ct);
    }

    public async Task<TenantPlanAssignment?> GetActiveAssignmentAsync(TenantId tenantId, DateTimeOffset at, CancellationToken ct = default)
    {
        return await _context.TenantPlanAssignments
            .Where(a => a.TenantId == tenantId && a.IsActive)
            .Where(a => a.EffectiveFrom <= at && (a.EffectiveTo == null || a.EffectiveTo >= at))
            .FirstOrDefaultAsync(ct);
    }

    // Usage
    public async Task AddUsageObservationAsync(UsageObservation observation, CancellationToken ct = default)
    {
        await _context.UsageObservations.AddAsync(observation, ct);
    }

    public async Task<decimal> GetUsageTotalAsync(TenantId tenantId, string meter, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        return await _context.UsageObservations
            .Where(o => o.TenantId == tenantId && o.Meter == meter)
            .Where(o => o.StartedAt >= from && o.EndedAt <= to)
            .SumAsync(o => o.Quantity, ct);
    }

    public async Task<IReadOnlyList<UsageObservation>> ListUsageAsync(TenantId tenantId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        return await _context.UsageObservations
            .Where(o => o.TenantId == tenantId)
            .Where(o => o.StartedAt >= from && o.EndedAt <= to)
            .ToListAsync(ct);
    }

    public async Task<UsageObservation?> GetUsageByIdempotencyKeyAsync(TenantId tenantId, string idempotencyKey, CancellationToken ct = default)
    {
        return await _context.UsageObservations
            .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.IdempotencyKey == idempotencyKey, ct);
    }
}
