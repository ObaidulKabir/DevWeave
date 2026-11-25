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
    IUsageMeter,
    ITenantTrainingPolicy
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
        var now = DateTimeOffset.UtcNow;
        var assignment = await _repository.GetActiveAssignmentAsync(scope.TenantId, now, ct);
        if (assignment == null)
        {
            return await RecordDecisionAsync(scope, null, null, entitlementKey, requested, 0,
                "Blocked", "No active commercial plan assigned to tenant.", ct);
        }

        var plan = await _repository.GetPlanByIdAsync(assignment.PlanId, ct);
        var rules = await _repository.GetRulesByPlanIdAsync(assignment.PlanId, ct);
        var rule = rules.FirstOrDefault(r => r.Key == entitlementKey);
        
        if (rule == null)
        {
            return await RecordDecisionAsync(scope, assignment.PlanId, plan?.Version, entitlementKey, requested, 0,
                "Allowed", "No limit defined for this entitlement.", ct);
        }

        // COM-002: Enforcement
        var currentUsage = await _repository.GetUsageTotalAsync(scope.TenantId, rule.Key, now.AddMonths(-1), now, ct);
        
        if (currentUsage + requested > rule.Limit)
        {
            var (outcome, reason) = rule.Action switch
            {
                EntitlementAction.Block => ("Blocked", $"Entitlement limit exceeded: {rule.Key}"),
                EntitlementAction.Warn => ("Warned", $"Entitlement limit approaching: {rule.Key}"),
                _ => ("Allowed", "Entitlement allowed despite limit.")
            };
            return await RecordDecisionAsync(scope, assignment.PlanId, plan?.Version, entitlementKey, requested, currentUsage, outcome, reason, ct);
        }

        return await RecordDecisionAsync(scope, assignment.PlanId, plan?.Version, entitlementKey, requested, currentUsage,
            "Allowed", "Entitlement within limits.", ct);
    }

    public async Task<Decision> EvaluateCapacityAsync(AuthorizedScope scope, string entitlementKey, decimal observed, decimal requested, CancellationToken ct = default)
    {
        var assignment = await _repository.GetActiveAssignmentAsync(scope.TenantId, DateTimeOffset.UtcNow, ct);
        if (assignment == null)
            return new Decision("Allowed", "No commercial plan; capacity rule is not in force.", "1.0");

        var plan = await _repository.GetPlanByIdAsync(assignment.PlanId, ct);
        var rules = await _repository.GetRulesByPlanIdAsync(assignment.PlanId, ct);
        var rule = rules.FirstOrDefault(r => r.Key == entitlementKey);
        if (rule == null)
            return new Decision("Allowed", "No capacity rule for this key.", "1.0");

        if (observed + requested > rule.Limit)
        {
            var (outcome, reason) = rule.Action switch
            {
                EntitlementAction.Block => ("Blocked", $"Concurrent capacity exceeded: {rule.Key}"),
                EntitlementAction.Warn => ("Warned", $"Concurrent capacity approaching: {rule.Key}"),
                _ => ("Allowed", "Capacity allowed despite limit.")
            };
            return await RecordDecisionAsync(scope, assignment.PlanId, plan?.Version, entitlementKey, requested, observed, outcome, reason, ct);
        }

        return await RecordDecisionAsync(scope, assignment.PlanId, plan?.Version, entitlementKey, requested, observed,
            "Allowed", "Capacity within limits.", ct);
    }

    public async Task<IReadOnlyList<EntitlementDecisionView>> ListDecisionsAsync(AuthorizedScope scope, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var rows = await _repository.ListEntitlementDecisionsAsync(scope.TenantId, from, to, ct);
        return rows.Select(row => new EntitlementDecisionView(
            row.Id.Value,
            row.PlanId?.Value,
            row.PlanVersion,
            row.Meter,
            row.Requested,
            row.Observed,
            row.Outcome,
            row.Reason,
            row.PolicyVersion,
            row.DecidedAt)).ToList();
    }

    public async Task<TrainingUseView> GetAsync(AuthorizedScope scope, CancellationToken ct = default)
    {
        var policy = await _repository.GetTrainingPolicyAsync(scope.TenantId, ct)
            ?? TenantTrainingPolicy.Excluded(scope.TenantId);
        return new TrainingUseView(policy.Use, policy.ActorId?.Value, policy.ChangedAt, policy.Reason);
    }

    public async Task OptInAsync(AuthorizedScope scope, string reason, CancellationToken ct = default)
    {
        var policy = await _repository.GetTrainingPolicyAsync(scope.TenantId, ct);
        if (policy is null)
        {
            policy = TenantTrainingPolicy.Excluded(scope.TenantId);
            policy.OptIn(scope.ActorId, reason, DateTimeOffset.UtcNow);
            await _repository.AddTrainingPolicyAsync(policy, ct);
        }
        else
        {
            policy.OptIn(scope.ActorId, reason, DateTimeOffset.UtcNow);
        }

        await _uow.SaveChangesAsync(ct);
    }

    private async Task<Decision> RecordDecisionAsync(
        AuthorizedScope scope,
        CommercialPlanId? planId,
        string? planVersion,
        string meter,
        decimal requested,
        decimal observed,
        string outcome,
        string reason,
        CancellationToken ct)
    {
        var decision = EntitlementDecision.Record(
            scope.TenantId,
            scope.ActorId,
            planId,
            planVersion,
            meter,
            requested,
            observed,
            outcome,
            reason,
            "1.0",
            DateTimeOffset.UtcNow);
        await _repository.AddEntitlementDecisionAsync(decision, ct);
        await _uow.SaveChangesAsync(ct);
        return new Decision(outcome, reason, "1.0");
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
