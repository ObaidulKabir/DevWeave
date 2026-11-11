namespace DevWeave.Contracts.Planned.V1;

// Draft contracts: wire identities are GUIDs; authenticated tenant/actor scope comes from
// the host, never from these request bodies. No endpoint implements these contracts yet.
public sealed record PageRequest(int Limit, string? Cursor);
public sealed record Page<T>(IReadOnlyList<T> Items, string? NextCursor);
public sealed record ResourceRef(Guid Id, Guid? RevisionId);
public sealed record Decision(string Outcome, string Reason, string PolicyVersion);
public sealed record DraftCommand(string Key, string Purpose, string IdempotencyKey);
public sealed record TransitionCommand(string TargetState, string Reason, int ExpectedVersion, string IdempotencyKey);
public sealed record ContractProblem(string Code, string Message, string CorrelationId, bool Retryable);
public sealed record PolicyCheck(string Action, ResourceRef Resource);
public sealed record SupportAccessDraft(Guid TargetTenantId, string Purpose, DateTimeOffset ExpiresAt);
public sealed record LifecycleTransitionDraft(Guid TargetId, TransitionCommand Transition);
public sealed record RetentionActionDraft(string DataCategory, string Operation);
public sealed record ExportDraft(string IdempotencyKey);
public sealed record ImportDraft(string ValidatedManifestRef, string IdempotencyKey);
public sealed record ReportDraft(string DeclaredPurpose);
public sealed record NotificationDraft(string EventType, string DestinationRef);

public sealed record WorkItemDraft(string Title, string Description, Guid? MilestoneId,
    IReadOnlyList<Guid> DependsOn, DateTimeOffset? DueAt, string Priority);
public sealed record WorkItemView(Guid Id, Guid ProjectId, string Title, string PlannedState,
    string ReportedState, string VerificationState, string ApprovalState, int Version);
public sealed record AuditView(Guid Id, Guid ActorId, string Action, string Target, string Outcome,
    DateTimeOffset RecordedAt, string CorrelationId);
public sealed record SearchQuery(string Text, Guid? ProjectId, string? TypeId, string? Lifecycle,
    string? Review, string? Verification, string? Currency, Guid? ActorId, DateTimeOffset? Since,
    int Limit, string? Cursor);
public sealed record SearchHit(ResourceRef Item, string Title, string TypeId, string State,
    DateTimeOffset RecordedAt, DateTimeOffset? IndexedAt, string ProvenanceRef);
public sealed record IntegrationDraft(string Provider, string Scope, string CredentialRef,
    IReadOnlyList<string> AllowedMetadataFields);
public sealed record IngressEnvelope(string ProviderEventId, DateTimeOffset SentAt,
    string SchemaVersion, string MetadataJson);
public sealed record ExportManifest(Guid TenantId, string FormatVersion, DateTimeOffset CreatedAt,
    IReadOnlyList<ResourceRef> Revisions, IReadOnlyList<string> ArtifactChecksums);
public sealed record MetricView(string Key, decimal Value, string Unit, string DefinitionVersion,
    DateTimeOffset CalculatedAt, string IncludedPopulation, string ExcludedPopulation);
public sealed record ApplicationDraft(string Key, string Version, string LifecycleState,
    IReadOnlyList<string> DeclaredCapabilities);
public sealed record AnnotationDraft(ResourceRef Target, string Text, string Classification);
public sealed record OperationsView(string Component, string Status, DateTimeOffset CheckedAt,
    string CorrelationId);

public sealed record BaselineDraft(string Goal, IReadOnlyList<ResourceRef> ApprovedRevisions,
    IReadOnlyList<Guid> Dependencies, IReadOnlyList<string> AcceptanceCriteria, int ExpectedPlanVersion);
public sealed record CodebaseTargetDraft(string Provider, string RepositoryId,
    string TargetBranch, string BaseCommitSha);
public sealed record CampaignDraft(Guid BaselineId, IReadOnlyList<Guid> CodebaseTargetIds,
    string Objective, string ExecutionMode, string IdempotencyKey);
public sealed record RuntimeRegistration(Guid AgentPrincipalId, string RuntimeClass,
    string ProtocolVersion, IReadOnlyList<string> Capabilities, string AttestationRef);
public sealed record OfferDraft(Guid CampaignId, Guid RuntimeId, IReadOnlyList<string> Capabilities,
    DateTimeOffset AvailableUntil);
public sealed record PackageDraft(Guid CampaignId, Guid BaselineId, string Objective,
    IReadOnlyList<Guid> DependsOn, IReadOnlyList<string> AllowedScopes,
    IReadOnlyList<string> AcceptanceCriteria);
public sealed record LeaseRequest(Guid WorkPackageId, Guid RuntimeId, string ResourceScope,
    DateTimeOffset ExpiresAt, long ExpectedFence, string IdempotencyKey);
public sealed record LeaseView(Guid Id, Guid WorkPackageId, Guid RuntimeId, string ResourceScope,
    long Fence, DateTimeOffset ExpiresAt, string State);
public sealed record ChangeSetDraft(Guid WorkPackageId, Guid CodebaseTargetId, string ProviderRef,
    string CommitSha, IReadOnlyList<string> ChangedPathMetadata);
public sealed record WorkspaceDraft(Guid WorkPackageId, string BaseCommitSha);
public sealed record QueueDraft(Guid ChangeSetId, string IdempotencyKey);
public sealed record ChannelDraft(string ChannelType, Guid? CampaignId, Guid? WorkPackageId);
public sealed record CursorDraft(Guid ChannelId, long ProcessedThroughSequence);
public sealed record MessageDraft(Guid ChannelId, string MessageType, string SchemaVersion,
    string AllowedMetadataJson, IReadOnlyList<ResourceRef> References, Guid? ParentMessageId,
    string IdempotencyKey);
public sealed record MessageView(Guid Id, Guid ChannelId, long Sequence, Guid SenderId,
    Guid RuntimeId, string MessageType, string SchemaVersion, string AllowedMetadataJson,
    DateTimeOffset CreatedAt);
public sealed record CoordinationEventView(Guid Id, Guid CampaignId, long Sequence,
    string EventType, DateTimeOffset RecordedAt, string CorrelationId);
public sealed record HandoffDraft(Guid WorkPackageId, Guid SourceRuntimeId, Guid TargetRuntimeId,
    string Reason);

public sealed record PlanDraft(string Key, string Version, DateTimeOffset EffectiveFrom,
    IReadOnlyList<EntitlementRule> Entitlements, decimal Price = 0, string Currency = "USD");
public sealed record EntitlementRule(string Key, decimal Limit, string Unit, string Action,
    decimal OverageRate = 0);
public sealed record EntitlementCheck(string EntitlementKey, decimal Requested);
public sealed record UsageObservation(Guid? ProjectId, Guid? CampaignId, Guid? RuntimeId,
    string ExecutionMode, string ResourceClass, string Meter, decimal Quantity,
    DateTimeOffset StartedAt, DateTimeOffset EndedAt, string IdempotencyKey);
public sealed record UsageView(string Meter, decimal Consumed, string ResourceClass,
    DateTimeOffset WindowStart, DateTimeOffset WindowEnd);
public sealed record ManagedRunnerRequest(Guid CampaignId, Guid WorkPackageId,
    string ComputeClass, string CredentialRef, DateTimeOffset ExpiresAt);
public sealed record BillingWindow(DateTimeOffset From, DateTimeOffset To, string IdempotencyKey);
