using DevWeave.Contracts.Planned.V1;

namespace DevWeave.Application.Abstractions.Planned;

// COM-001..026: catalogue and metering are vendor-neutral; financial authority remains separate
// from reported execution, and customer-managed model spend is never inferred as platform usage.
public interface IPlanCatalogue
{
    Task<Guid> PublishAsync(AuthorizedScope scope, PlanDraft draft, CancellationToken ct = default);
    Task<Guid> AssignAsync(AuthorizedScope scope, Guid tenantId, Guid planId,
        DateTimeOffset effectiveAt, CancellationToken ct = default);
}

public interface IEntitlementEvaluator
{
    Task<Decision> EvaluateAsync(AuthorizedScope scope, string entitlementKey, decimal requested,
        CancellationToken ct = default);
}

public interface IUsageMeter
{
    Task<Guid> RecordAsync(AuthorizedScope scope, UsageObservation observation,
        CancellationToken ct = default);
    Task<IReadOnlyList<UsageView>> SummarizeAsync(AuthorizedScope scope,
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}

public interface IManagedRuntimeProvider
{
    Task<string> ProvisionAsync(AuthorizedScope scope, ManagedRunnerRequest request,
        CancellationToken ct = default);
    Task RevokeAsync(AuthorizedScope scope, string runtimeRef, CancellationToken ct = default);
}

public interface IBillingReconciliation
{
    Task<Guid> ReconcileAsync(AuthorizedScope scope, DateTimeOffset from,
        DateTimeOffset to, string idempotencyKey, CancellationToken ct = default);
}
