namespace DevWeave.Infrastructure.Persistence.Planned;

public sealed record PlannedColumn(string Name, Type ClrType, bool Required = true, int? MaxLength = null);
public sealed record PlannedTable(string Name, bool TenantScoped, bool ProjectScoped,
    IReadOnlyList<PlannedColumn> Columns, string[]? UniqueScope = null);

/// <summary>
/// Review-only physical schema proposal spanning the still-unimplemented SRS domains.
/// Existing Slice 1-8 tables remain owned by DevWeaveDbContext. These shapes must be reconciled
/// with FK/RLS, classification and outbox ADRs before any production deployment.
/// </summary>
public static class PlannedSchemaCatalog
{
    private static PlannedColumn Text(string name, int length = 200, bool required = true) =>
        new(name, typeof(string), required, length);
    private static PlannedColumn Id(string name, bool required = true) =>
        new(name, required ? typeof(Guid) : typeof(Guid?), required);
    private static PlannedColumn Time(string name, bool required = true) =>
        new(name, required ? typeof(DateTimeOffset) : typeof(DateTimeOffset?), required);
    private static PlannedColumn Number(string name) => new(name, typeof(long));
    private static PlannedColumn Amount(string name) => new(name, typeof(decimal));
    private static PlannedTable Project(string name, string[]? unique = null, params PlannedColumn[] fields) =>
        new(name, true, true, fields, unique);
    private static PlannedTable Tenant(string name, string[]? unique = null, params PlannedColumn[] fields) =>
        new(name, true, false, fields, unique);

    public static IReadOnlyList<PlannedTable> Tables { get; } =
    [
        // Foundation: LFC/GOV/INT/SRH/RPT/EXP/OPS. No binary, source or credential values.
        Project("work_items", ["ProjectId", "Key"], Text("Key", 100), Text("Title"),
            Text("PlannedState", 40), Text("ReportedState", 40), Text("VerifiedState", 40),
            Text("ApprovedState", 40), Id("OwnerId", false), Time("DueAt", false), Number("Version")),
        Project("milestones", ["ProjectId", "Key"], Text("Key", 100), Text("Title"), Time("DueAt", false)),
        Project("work_dependencies", ["ProjectId", "WorkItemId", "DependsOnId"], Id("WorkItemId"), Id("DependsOnId")),
        Project("lifecycle_transitions", null, Id("TargetId"), Id("ActorId"), Text("FromState", 40),
            Text("ToState", 40), Text("Reason", 1000), Time("RecordedAt")),
        Tenant("authorization_grants", null, Id("SubjectId"), Id("GrantedBy"), Id("ProjectId", false),
            Id("ResourceId", false), Text("Capability", 100), Text("ClassificationCeiling", 40),
            Time("ExpiresAt", false), Time("RevokedAt", false)),
        Tenant("support_access_grants", null, Id("OperatorId"), Id("ApproverId", false),
            Text("Purpose", 500), Time("ExpiresAt"), Time("RevokedAt", false)),
        Tenant("audit_entries", null, Id("ActorId"), Text("Action", 100), Text("Target", 200),
            Text("Outcome", 40), Text("CorrelationId", 100), Time("RecordedAt")),
        Tenant("outbox_messages", ["TenantId", "EventKey"], Text("EventKey", 150),
            Text("EventType", 100), Text("SchemaVersion", 40), Text("PayloadRef", 500),
            Time("PublishedAt", false), Number("Attempts")),
        Project("search_documents", ["ProjectId", "ItemId"], Id("ItemId"), Id("RevisionId"),
            Text("TypeId", 100), Text("IndexState", 40), Time("IndexedAt", false)),
        Tenant("integration_connections", ["TenantId", "Provider", "ExternalAccountId"],
            Text("Provider", 100), Text("ExternalAccountId", 200), Text("CredentialRef", 500),
            Text("State", 40), Time("LastSyncAt", false)),
        Project("external_references", ["ProjectId", "Provider", "ExternalId"],
            Text("Provider", 100), Text("ExternalId", 200), Text("ExternalUri", 1000),
            Text("ResourceType", 80), Text("SyncState", 40)),
        Tenant("ingress_receipts", ["TenantId", "ConnectionId", "ProviderEventId"],
            Id("ConnectionId"), Text("ProviderEventId", 200), Time("ReceivedAt"),
            Text("Disposition", 40)),
        Tenant("webhook_subscriptions", null, Text("EventType", 100), Text("DestinationRef", 500),
            Text("State", 40)),
        Tenant("export_jobs", null, Id("RequestedBy"), Text("ManifestRef", 500, false),
            Text("State", 40), Text("FormatVersion", 40)),
        Tenant("import_jobs", null, Id("RequestedBy"), Text("ManifestRef", 500),
            Text("State", 40), Text("FormatVersion", 40)),
        Project("metric_snapshots", ["ProjectId", "MetricKey", "DefinitionVersion", "CalculatedAt"],
            Text("MetricKey", 100), Text("DefinitionVersion", 40), Amount("Value"),
            Text("Unit", 40), Text("PopulationRef", 500), Time("CalculatedAt")),
        Project("report_snapshots", null, Id("RequestedBy"), Text("Purpose", 200),
            Text("ContentRef", 500), Text("EpistemicLabel", 40)),
        Tenant("notification_receipts", null, Id("RecipientId"), Text("EventType", 100),
            Text("DestinationRef", 500), Text("State", 40)),
        Project("annotations", null, Id("ItemId"), Id("RevisionId"),
            Id("AuthorId"), Text("ContentRef", 500), Text("Classification", 40)),
        Project("review_assignments", null, Id("ItemId"), Id("RevisionId"),
            Id("ReviewerId"), Time("DueAt", false), Text("State", 40)),
        Tenant("notification_preferences", ["TenantId", "PrincipalId", "EventType"],
            Id("PrincipalId"), Text("EventType", 100), Text("DeliveryMode", 40)),
        new PlannedTable("application_catalogue", false, false,
            [Text("Key", 100), Text("Version", 40), Text("State", 40),
                Text("CapabilityManifestRef", 500)], ["Key", "Version"]),
        Tenant("tenant_applications", ["TenantId", "ApplicationId"], Id("ApplicationId"),
            Text("State", 40)),
        Tenant("feature_flags", ["TenantId", "Key"], Text("Key", 100),
            Text("Variant", 100), Time("ExpiresAt", false)),
        Tenant("backup_jobs", null, Id("RequestedBy"), Text("ScopeKey", 100),
            Text("State", 40), Text("ManifestRef", 500, false)),
        Tenant("retention_policies", ["TenantId", "Category"], Text("Category", 100),
            Number("RetentionDays"), Time("HoldUntil", false)),

        // Coordination: MCO-001..048; external repository content never enters this schema.
        Project("plan_baselines", ["ProjectId", "Version"], Id("LockedBy"), Number("Version"),
            Text("Goal", 1000), Text("State", 40), Time("LockedAt")),
        Project("baseline_revisions", ["ProjectId", "BaselineId", "ItemId", "RevisionId"],
            Id("BaselineId"), Id("ItemId"), Id("RevisionId")),
        Project("codebase_targets", ["ProjectId", "Provider", "RepositoryId", "TargetBranch"],
            Text("Provider", 100), Text("RepositoryId", 200), Text("TargetBranch", 200),
            Text("BaseCommitSha", 100), Text("IntegrationPolicyRef", 500)),
        Project("execution_campaigns", null, Id("BaselineId"), Text("Objective", 1000),
            Text("ExecutionMode", 40), Text("State", 40), Number("Version")),
        Project("agent_runtime_instances", ["ProjectId", "SessionKey"], Id("PrincipalId"),
            Text("SessionKey", 200), Text("RuntimeClass", 80), Text("ProtocolVersion", 40),
            Text("State", 40), Time("LastHeartbeatAt", false)),
        Project("participation_offers", null, Id("CampaignId"), Id("RuntimeId"),
            Text("State", 40), Time("AvailableUntil")),
        Project("work_packages", ["ProjectId", "CampaignId", "Key"], Id("CampaignId"),
            Id("BaselineId"), Text("Key", 100), Text("Objective", 1000),
            Text("State", 40), Number("Version")),
        Project("task_leases", null, Id("WorkPackageId"), Id("RuntimeId"),
            Text("ResourceScope", 300), Number("Fence"), Time("ExpiresAt"), Text("State", 40)),
        Project("resource_reservations", null, Id("CampaignId"), Id("RuntimeId"),
            Text("ResourceScope", 300), Time("ExpiresAt"), Text("State", 40)),
        Project("workspace_references", null, Id("WorkPackageId"), Id("RuntimeId"),
            Text("ExternalRef", 500), Text("BaseCommitSha", 100)),
        Project("change_set_references", ["ProjectId", "Provider", "ExternalId"],
            Id("WorkPackageId"), Id("CodebaseTargetId"), Text("Provider", 100),
            Text("ExternalId", 200), Text("CommitSha", 100), Text("MetadataRef", 500)),
        Project("integration_queue_items", null, Id("ChangeSetId"),
            Text("State", 40), Number("Fence"), Text("PolicyVersion", 40)),

        // MCP: durable ordered messaging and cursor replay; presence/subscriptions are ephemeral.
        Project("mcp_service_configs", ["ProjectId"], Text("EndpointRef", 500),
            Text("ProtocolProfile", 60), Text("PolicyVersion", 40), Text("State", 40)),
        Project("coordination_channels", null, Id("CampaignId", false),
            Id("WorkPackageId", false), Text("ChannelType", 60), Text("Visibility", 40),
            Number("SequenceHighWatermark")),
        Project("coordination_messages", ["ProjectId", "ChannelId", "Sequence"],
            Id("ChannelId"), Id("SenderId"), Id("RuntimeId"), Id("ParentMessageId", false),
            Number("Sequence"), Text("MessageType", 80), Text("SchemaVersion", 40),
            Text("PayloadRef", 500), Text("IdempotencyKey", 150)),
        Project("coordination_cursors", ["ProjectId", "ChannelId", "PrincipalId"],
            Id("ChannelId"), Id("PrincipalId"), Number("LastReadSequence"),
            Number("LastAcknowledgedSequence")),
        Project("handoff_requests", null, Id("WorkPackageId"), Id("SourceRuntimeId"),
            Id("TargetRuntimeId"), Text("Reason", 1000), Text("State", 40)),
        Project("coordination_events", ["ProjectId", "CampaignId", "EventKey"],
            Id("CampaignId"), Id("ChannelId", false), Text("EventKey", 150),
            Text("EventType", 100), Text("PayloadRef", 500), Number("Sequence")),

        // Commercial: COM-001..026. Catalogue is global; subscriptions and usage are tenant scoped.
        new PlannedTable("commercial_plans", false, false,
            [Text("Key", 100), Text("Version", 40), Text("State", 40), Time("EffectiveFrom")],
            ["Key", "Version"]),
        Tenant("tenant_subscriptions", null, Id("PlanId"), Time("EffectiveFrom"),
            Time("EffectiveUntil", false), Text("State", 40)),
        Tenant("entitlement_rules", ["TenantId", "PlanId", "Key"], Id("PlanId"),
            Text("Key", 100), Amount("Limit"), Text("Unit", 40), Text("Action", 40)),
        Tenant("usage_records", ["TenantId", "IdempotencyKey"], Id("ProjectId", false),
            Id("CampaignId", false), Id("RuntimeId", false), Text("ExecutionMode", 40),
            Text("ResourceClass", 80), Text("Meter", 100), Amount("Quantity"),
            Time("StartedAt"), Time("EndedAt"), Text("IdempotencyKey", 150)),
        Project("managed_runners", null, Id("CampaignId"), Id("WorkPackageId"),
            Text("ProviderRef", 500), Text("ComputeClass", 80), Text("State", 40),
            Time("ExpiresAt")),
        Tenant("billing_reconciliations", ["TenantId", "IdempotencyKey"],
            Text("IdempotencyKey", 150), Time("WindowStart"), Time("WindowEnd"),
            Text("State", 40), Text("ResultRef", 500, false)),
    ];
}
