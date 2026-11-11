using DevWeave.Application.Abstractions;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Contracts.Planned.V1;
using DevWeave.Domain.Commercial;
using DevWeave.Domain.Identity;
using DomainEntitlementRule = DevWeave.Domain.Commercial.EntitlementRule;
using DomainUsageObservation = DevWeave.Domain.Commercial.UsageObservation;
using ContractUsageObservation = DevWeave.Contracts.Planned.V1.UsageObservation;

namespace DevWeave.Application.Commercial;

public sealed class CommercialService : 
    IPlanCatalogue, 
    IEntitlementEvaluator, 
    IUsageMeter
{
    private readonly ICommercialRepository _repository;
    private readonly IUnitOfWork _uow;

    public CommercialService(ICommercialRepository repository, IUnitOfWork uow)
    {
        _repository = repository;
        _uow = uow;
    }

    // --- IPlanCatalogue ---

    public async Task<Guid> PublishAsync(AuthorizedScope scope, PlanDraft draft, CancellationToken ct = default)
    {
        // COM-001: Versioned plans
        var plan = CommercialPlan.Create(
            draft.Key,
            draft.Version,
            draft.EffectiveFrom,
            draft.Price,
            draft.Currency);
        
        await _repository.AddPlanAsync(plan, ct);

        foreach (var ruleDraft in draft.Entitlements)
        {
            var rule = DomainEntitlementRule.Create(
                plan.Id,
                ruleDraft.Key,
                ruleDraft.Limit,
                ruleDraft.Unit,
                Enum.Parse<EntitlementAction>(ruleDraft.Action),
                ruleDraft.OverageRate);
            
            await _repository.AddEntitlementRuleAsync(rule, ct);
        }

        await _uow.SaveChangesAsync(ct);
        return plan.Id.Value;
    }

    public async Task<Guid> AssignAsync(AuthorizedScope scope, Guid tenantId, Guid planId, DateTimeOffset effectiveAt, CancellationToken ct = default)
    {
        var tenantPlanId = new TenantId(tenantId);
        var commercialPlanId = new CommercialPlanId(planId);

        // Deactivate current active assignment if any
        var current = await _repository.GetActiveAssignmentAsync(tenantPlanId, effectiveAt, ct);
        current?.Deactivate(effectiveAt);

        var assignment = TenantPlanAssignment.Assign(tenantPlanId, commercialPlanId, effectiveAt);
        await _repository.AddAssignmentAsync(assignment, ct);
        
        await _uow.SaveChangesAsync(ct);
        return assignment.Id.Value;
    }

    // --- IEntitlementEvaluator ---

    public async Task<Decision> EvaluateAsync(AuthorizedScope scope, string entitlementKey, decimal requested, CancellationToken ct = default)
    {
        var assignment = await _repository.GetActiveAssignmentAsync(scope.TenantId, DateTimeOffset.UtcNow, ct);
        if (assignment == null)
        {
            return new Decision("Blocked", "No active commercial plan assigned to tenant.", "1.0");
        }

        var rules = await _repository.GetRulesByPlanIdAsync(assignment.PlanId, ct);
        var rule = rules.FirstOrDefault(r => r.Key == entitlementKey);
        
        if (rule == null)
        {
            return new Decision("Allowed", "No limit defined for this entitlement.", "1.0");
        }

        // COM-002: Enforcement
        var currentUsage = await _repository.GetUsageTotalAsync(scope.TenantId, rule.Key, DateTimeOffset.UtcNow.AddMonths(-1), DateTimeOffset.UtcNow, ct);
        
        if (currentUsage + requested > rule.Limit)
        {
            return rule.Action switch
            {
                EntitlementAction.Block => new Decision("Blocked", $"Entitlement limit exceeded: {rule.Key}", "1.0"),
                EntitlementAction.Warn => new Decision("Warned", $"Entitlement limit approaching: {rule.Key}", "1.0"),
                _ => new Decision("Allowed", "Entitlement allowed despite limit.", "1.0")
            };
        }

        return new Decision("Allowed", "Entitlement within limits.", "1.0");
    }

    // --- IUsageMeter ---

    public async Task<Guid> RecordAsync(AuthorizedScope scope, ContractUsageObservation draft, CancellationToken ct = default)
    {
        // Idempotency check (COM-003)
        var existing = await _repository.GetUsageByIdempotencyKeyAsync(scope.TenantId, draft.IdempotencyKey, ct);
        if (existing != null) return existing.Id.Value;

        var observation = DomainUsageObservation.Record(
            scope.TenantId,
            draft.ProjectId.HasValue ? new DevWeave.Domain.Projects.ProjectId(draft.ProjectId.Value) : null,
            draft.CampaignId.HasValue ? new DevWeave.Domain.Coordination.CampaignId(draft.CampaignId.Value) : null,
            draft.RuntimeId.HasValue ? new DevWeave.Domain.Coordination.RuntimeId(draft.RuntimeId.Value) : null,
            draft.ExecutionMode,
            draft.ResourceClass,
            draft.Meter,
            draft.Quantity,
            draft.StartedAt,
            draft.EndedAt,
            draft.IdempotencyKey);

        await _repository.AddUsageObservationAsync(observation, ct);
        await _uow.SaveChangesAsync(ct);
        return observation.Id.Value;
    }

    public async Task<IReadOnlyList<UsageView>> SummarizeAsync(AuthorizedScope scope, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var observations = await _repository.ListUsageAsync(scope.TenantId, from, to, ct);
        
        return observations
            .GroupBy(o => new { o.Meter, o.ResourceClass })
            .Select(g => new UsageView(
                g.Key.Meter,
                g.Sum(o => o.Quantity),
                g.Key.ResourceClass,
                from,
                to))
            .ToList();
    }
}
