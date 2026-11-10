using DevWeave.Contracts.Planned.V1;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Abstractions.Planned;

/// <summary>
/// Scope must be derived from authenticated identity and re-evaluated on every request.
/// It is a contract, not an authorization implementation (TEN-007, GOV-001..005).
/// </summary>
public sealed record AuthorizedScope(TenantId TenantId, PrincipalId ActorId, ProjectId? ProjectId,
    string Purpose, string CorrelationId);

public interface IResourcePolicy
{
    Task<Decision> EvaluateAsync(AuthorizedScope scope, string action, ResourceRef resource,
        CancellationToken ct = default);
}

public interface ISupportAccess
{
    Task<Guid> RequestAsync(AuthorizedScope scope, Guid targetTenantId, string purpose,
        DateTimeOffset expiresAt, CancellationToken ct = default);
    Task<Decision> ApproveAsync(AuthorizedScope scope, Guid requestId, CancellationToken ct = default);
    Task RevokeAsync(AuthorizedScope scope, Guid grantId, CancellationToken ct = default);
}

public interface IWorkPlanning
{
    Task<WorkItemView> CreateAsync(AuthorizedScope scope, WorkItemDraft draft, CancellationToken ct = default);
    Task<WorkItemView> TransitionAsync(AuthorizedScope scope, Guid workItemId,
        TransitionCommand command, CancellationToken ct = default);
    Task<Page<WorkItemView>> ListAsync(AuthorizedScope scope, PageRequest page, CancellationToken ct = default);
}

public interface ILifecycleGovernance
{
    Task<Decision> CheckGateAsync(AuthorizedScope scope, Guid targetId, string gate,
        CancellationToken ct = default);
    Task<Guid> RecordTransitionAsync(AuthorizedScope scope, Guid targetId,
        TransitionCommand command, CancellationToken ct = default);
}

public interface IAuditJournal
{
    Task AppendAsync(AuthorizedScope scope, string action, string target, string outcome,
        CancellationToken ct = default);
    Task<Page<AuditView>> ListAsync(AuthorizedScope scope, PageRequest page,
        CancellationToken ct = default);
}

public interface ITransactionalOutbox
{
    // Called within the same unit of work as the mutation; successful publication is not implied.
    Task EnqueueAsync(AuthorizedScope scope, string eventType, string schemaVersion,
        string allowlistedPayloadJson, CancellationToken ct = default);
}

public interface IKnowledgeSearch
{
    Task<Page<SearchHit>> SearchAsync(AuthorizedScope scope, SearchQuery query,
        CancellationToken ct = default);
    Task<DateTimeOffset?> IndexedThroughAsync(AuthorizedScope scope, CancellationToken ct = default);
}

public interface IIntegrationRegistry
{
    Task<Guid> ConnectAsync(AuthorizedScope scope, IntegrationDraft draft,
        CancellationToken ct = default);
    Task RotateCredentialAsync(AuthorizedScope scope, Guid connectionId, string credentialRef,
        CancellationToken ct = default);
    Task DisconnectAsync(AuthorizedScope scope, Guid connectionId, CancellationToken ct = default);
}

public interface IInboundEventProcessor
{
    // The adapter verifies signed headers and freshness before invoking this port.
    Task<Decision> AcceptAsync(AuthorizedScope scope, Guid connectionId, IngressEnvelope envelope,
        CancellationToken ct = default);
}

public interface IPortableArchive
{
    Task<ExportManifest> DescribeExportAsync(AuthorizedScope scope, CancellationToken ct = default);
    Task<Guid> BeginExportAsync(AuthorizedScope scope, string idempotencyKey,
        CancellationToken ct = default);
    Task<Guid> BeginImportAsync(AuthorizedScope scope, string validatedManifestRef,
        string idempotencyKey, CancellationToken ct = default);
}

public interface IProjectReporting
{
    Task<IReadOnlyList<MetricView>> GetMetricsAsync(AuthorizedScope scope,
        DateTimeOffset asOf, CancellationToken ct = default);
    Task<Guid> GenerateReportAsync(AuthorizedScope scope, string declaredPurpose,
        CancellationToken ct = default);
}

public interface INotificationDelivery
{
    Task<Guid> SubscribeAsync(AuthorizedScope scope, string eventType, string destinationRef,
        CancellationToken ct = default);
    Task RevokeAsync(AuthorizedScope scope, Guid subscriptionId, CancellationToken ct = default);
}

public interface ICollaboration
{
    Task<Guid> AnnotateAsync(AuthorizedScope scope, AnnotationDraft draft,
        CancellationToken ct = default);
    Task<Guid> AssignReviewAsync(AuthorizedScope scope, ResourceRef target, PrincipalId reviewer,
        DateTimeOffset? dueAt, CancellationToken ct = default);
}

public interface IApplicationCatalogue
{
    Task<Guid> RegisterAsync(AuthorizedScope scope, ApplicationDraft draft,
        CancellationToken ct = default);
    Task<Decision> SetTenantAvailabilityAsync(AuthorizedScope scope, Guid applicationId,
        bool enabled, CancellationToken ct = default);
}

public interface IPlatformOperations
{
    Task<IReadOnlyList<OperationsView>> ReadinessAsync(CancellationToken ct = default);
    Task<Guid> RequestBackupAsync(AuthorizedScope scope, string scopeKey,
        CancellationToken ct = default);
    Task<Decision> CheckQuotaAsync(AuthorizedScope scope, string dimension, decimal requested,
        CancellationToken ct = default);
}

public interface IRetentionAndErasure
{
    Task<Guid> RequestAsync(AuthorizedScope scope, string dataCategory,
        string operation, CancellationToken ct = default);
    Task<Decision> CheckLegalHoldAsync(AuthorizedScope scope, string dataCategory,
        CancellationToken ct = default);
}
