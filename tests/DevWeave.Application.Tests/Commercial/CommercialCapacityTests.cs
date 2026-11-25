using DevWeave.Application.Abstractions;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Application.Commercial;
using DevWeave.Domain.Commercial;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using DomainRule = DevWeave.Domain.Commercial.EntitlementRule;
using DomainUsage = DevWeave.Domain.Commercial.UsageObservation;

namespace DevWeave.Application.Tests.Commercial;

public sealed class CommercialCapacityTests
{
    [Fact]
    public async Task COM_023_COM_024_a_project_capacity_block_is_stored_with_the_plan()
    {
        var tenant = TenantId.New();
        var actor = PrincipalId.New();
        var store = new MemoryCommercial();
        var plan = CommercialPlan.Create("standard", "1.0", DateTimeOffset.UtcNow, 0, "USD");
        store.Plans.Add(plan);
        store.Rules.Add(DomainRule.Create(plan.Id, "campaigns.concurrent.project", 1, "count", EntitlementAction.Block));
        store.Assignments.Add(TenantPlanAssignment.Assign(tenant, plan.Id, DateTimeOffset.UtcNow.AddMinutes(-1)));
        var service = new CommercialService(store, new MemoryUnitOfWork());
        var scope = new AuthorizedScope(tenant, actor, ProjectId.New(), "Create campaign", "trace");

        var decision = await service.EvaluateCapacityAsync(scope, "campaigns.concurrent.project", 1, 1);

        Assert.Equal("Blocked", decision.Outcome);
        var stored = Assert.Single(store.Decisions);
        Assert.Equal(plan.Id, stored.PlanId);
        Assert.Equal("1.0", stored.PlanVersion);
        Assert.Equal("campaigns.concurrent.project", stored.Meter);
        Assert.Equal(1, stored.Observed);
        Assert.Equal(actor, stored.ActorId);
    }

    [Fact]
    public async Task COM_023_a_missing_capacity_rule_leaves_the_campaign_allowed()
    {
        var tenant = TenantId.New();
        var store = new MemoryCommercial();
        var service = new CommercialService(store, new MemoryUnitOfWork());
        var scope = new AuthorizedScope(tenant, PrincipalId.New(), ProjectId.New(), "Create campaign", "trace");

        var decision = await service.EvaluateCapacityAsync(scope, "campaigns.concurrent", 4, 1);

        Assert.Equal("Allowed", decision.Outcome);
        Assert.Empty(store.Decisions);
    }

    [Fact]
    public void COM_020_export_does_not_consult_a_commercial_plan()
    {
        var parameters = typeof(DevWeave.Application.Archives.PortableArchive)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType.Name);

        Assert.DoesNotContain(parameters, name =>
            name.Contains("Commercial", StringComparison.Ordinal) ||
            name.Contains("Entitlement", StringComparison.Ordinal) ||
            name.Contains("PlanCatalogue", StringComparison.Ordinal));
    }

    private sealed class MemoryUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
    }

    private sealed class MemoryCommercial : ICommercialRepository
    {
        public List<CommercialPlan> Plans { get; } = new();
        public List<DomainRule> Rules { get; } = new();
        public List<TenantPlanAssignment> Assignments { get; } = new();
        public List<EntitlementDecision> Decisions { get; } = new();

        public Task AddPlanAsync(CommercialPlan plan, CancellationToken ct = default)
        {
            Plans.Add(plan);
            return Task.CompletedTask;
        }

        public Task<CommercialPlan?> GetPlanByIdAsync(CommercialPlanId planId, CancellationToken ct = default) =>
            Task.FromResult(Plans.FirstOrDefault(plan => plan.Id == planId));

        public Task<CommercialPlan?> GetPlanByKeyAsync(string key, string version, CancellationToken ct = default) =>
            Task.FromResult(Plans.FirstOrDefault(plan => plan.Key == key && plan.Version == version));

        public Task AddEntitlementRuleAsync(DomainRule rule, CancellationToken ct = default)
        {
            Rules.Add(rule);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<DomainRule>> GetRulesByPlanIdAsync(CommercialPlanId planId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DomainRule>>(Rules.Where(rule => rule.PlanId == planId).ToList());

        public Task AddAssignmentAsync(TenantPlanAssignment assignment, CancellationToken ct = default)
        {
            Assignments.Add(assignment);
            return Task.CompletedTask;
        }

        public Task<TenantPlanAssignment?> GetActiveAssignmentAsync(TenantId tenantId, DateTimeOffset at, CancellationToken ct = default) =>
            Task.FromResult(Assignments.FirstOrDefault(assignment =>
                assignment.TenantId == tenantId && assignment.IsActive &&
                assignment.EffectiveFrom <= at &&
                (assignment.EffectiveTo == null || assignment.EffectiveTo >= at)));

        public Task AddUsageObservationAsync(DomainUsage observation, CancellationToken ct = default) => Task.CompletedTask;

        public Task<decimal> GetUsageTotalAsync(TenantId tenantId, string meter, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default) =>
            Task.FromResult(0m);

        public Task<IReadOnlyList<DomainUsage>> ListUsageAsync(TenantId tenantId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DomainUsage>>(Array.Empty<DomainUsage>());

        public Task<DomainUsage?> GetUsageByIdempotencyKeyAsync(TenantId tenantId, string idempotencyKey, CancellationToken ct = default) =>
            Task.FromResult<DomainUsage?>(null);

        public Task AddEntitlementDecisionAsync(EntitlementDecision decision, CancellationToken ct = default)
        {
            Decisions.Add(decision);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<EntitlementDecision>> ListEntitlementDecisionsAsync(TenantId tenantId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<EntitlementDecision>>(Decisions.Where(decision => decision.TenantId == tenantId).ToList());

        public Task<TenantTrainingPolicy?> GetTrainingPolicyAsync(TenantId tenantId, CancellationToken ct = default) =>
            Task.FromResult<TenantTrainingPolicy?>(null);

        public Task AddTrainingPolicyAsync(TenantTrainingPolicy policy, CancellationToken ct = default) => Task.CompletedTask;
    }
}
