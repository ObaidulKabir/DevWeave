using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevWeave.Infrastructure.Persistence.Planned.Migrations
{
    /// <inheritdoc />
    public partial class ProposedFullSrsContracts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_runtime_instances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastHeartbeatAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PrincipalId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProtocolVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RuntimeClass = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SessionKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_runtime_instances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "audit_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Target = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "authorization_grants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Capability = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ClassificationCeiling = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    GrantedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_authorization_grants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "baseline_revisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BaselineId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_baseline_revisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "billing_reconciliations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ResultRef = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WindowEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    WindowStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_reconciliations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "change_set_references",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CodebaseTargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommitSha = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MetadataRef = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkPackageId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_change_set_references", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "codebase_targets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseCommitSha = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IntegrationPolicyRef = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RepositoryId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TargetBranch = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_codebase_targets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "commercial_plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Version = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_commercial_plans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "coordination_channels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChannelType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceHighWatermark = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Visibility = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    WorkPackageId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coordination_channels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "coordination_cursors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastAcknowledgedSequence = table.Column<long>(type: "bigint", nullable: false),
                    LastReadSequence = table.Column<long>(type: "bigint", nullable: false),
                    PrincipalId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coordination_cursors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "coordination_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EventKey = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PayloadRef = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coordination_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "coordination_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    MessageType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ParentMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    PayloadRef = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuntimeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coordination_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "entitlement_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Limit = table.Column<decimal>(type: "numeric", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Unit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entitlement_rules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "execution_campaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BaselineId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExecutionMode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Objective = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_execution_campaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "export_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FormatVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ManifestRef = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RequestedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_export_jobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "external_references",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExternalUri = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ResourceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SyncState = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_references", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "handoff_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SourceRuntimeId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TargetRuntimeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkPackageId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_handoff_requests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "import_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FormatVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ManifestRef = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_jobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ingress_receipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Disposition = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ProviderEventId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingress_receipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "integration_connections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CredentialRef = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ExternalAccountId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LastSyncAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_connections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "integration_queue_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangeSetId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Fence = table.Column<long>(type: "bigint", nullable: false),
                    PolicyVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_queue_items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "lifecycle_transitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FromState = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToState = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lifecycle_transitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "managed_runners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    ComputeClass = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderRef = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkPackageId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_managed_runners", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "mcp_service_configs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndpointRef = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PolicyVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProtocolProfile = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mcp_service_configs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "metric_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CalculatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DefinitionVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    MetricKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PopulationRef = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Unit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Value = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_metric_snapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "milestones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_milestones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "notification_receipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DestinationRef = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RecipientId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_receipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Attempts = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EventKey = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PayloadRef = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SchemaVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "participation_offers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AvailableUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuntimeId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_participation_offers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "plan_baselines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Goal = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    LockedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LockedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_baselines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "report_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentRef = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EpistemicLabel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_snapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "resource_reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceScope = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    RuntimeId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resource_reservations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "retention_policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    HoldUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RetentionDays = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_retention_policies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "search_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IndexState = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IndexedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TypeId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_search_documents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "support_access_grants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApproverId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OperatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_support_access_grants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "task_leases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Fence = table.Column<long>(type: "bigint", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceScope = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    RuntimeId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkPackageId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_leases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EffectiveUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_subscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "usage_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExecutionMode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Meter = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    ResourceClass = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    RuntimeId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usage_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "webhook_subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DestinationRef = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_subscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "work_dependencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DependsOnId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_dependencies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "work_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedState = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlannedState = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportedState = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    VerifiedState = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "work_packages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BaselineId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Objective = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_packages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "workspace_references",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseCommitSha = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExternalRef = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuntimeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkPackageId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_references", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_runtime_instances_Id_TenantId",
                table: "agent_runtime_instances",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agent_runtime_instances_ProjectId_SessionKey",
                table: "agent_runtime_instances",
                columns: new[] { "ProjectId", "SessionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agent_runtime_instances_TenantId",
                table: "agent_runtime_instances",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_agent_runtime_instances_TenantId_ProjectId",
                table: "agent_runtime_instances",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_entries_Id_TenantId",
                table: "audit_entries",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_entries_TenantId",
                table: "audit_entries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_authorization_grants_Id_TenantId",
                table: "authorization_grants",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_authorization_grants_TenantId",
                table: "authorization_grants",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_baseline_revisions_Id_TenantId",
                table: "baseline_revisions",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_baseline_revisions_ProjectId_BaselineId_ItemId_RevisionId",
                table: "baseline_revisions",
                columns: new[] { "ProjectId", "BaselineId", "ItemId", "RevisionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_baseline_revisions_TenantId",
                table: "baseline_revisions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_baseline_revisions_TenantId_ProjectId",
                table: "baseline_revisions",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_billing_reconciliations_Id_TenantId",
                table: "billing_reconciliations",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_billing_reconciliations_TenantId",
                table: "billing_reconciliations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_billing_reconciliations_TenantId_IdempotencyKey",
                table: "billing_reconciliations",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_change_set_references_Id_TenantId",
                table: "change_set_references",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_change_set_references_ProjectId_Provider_ExternalId",
                table: "change_set_references",
                columns: new[] { "ProjectId", "Provider", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_change_set_references_TenantId",
                table: "change_set_references",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_change_set_references_TenantId_ProjectId",
                table: "change_set_references",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_codebase_targets_Id_TenantId",
                table: "codebase_targets",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_codebase_targets_ProjectId_Provider_RepositoryId_TargetBran~",
                table: "codebase_targets",
                columns: new[] { "ProjectId", "Provider", "RepositoryId", "TargetBranch" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_codebase_targets_TenantId",
                table: "codebase_targets",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_codebase_targets_TenantId_ProjectId",
                table: "codebase_targets",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_commercial_plans_Key_Version",
                table: "commercial_plans",
                columns: new[] { "Key", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_coordination_channels_Id_TenantId",
                table: "coordination_channels",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_coordination_channels_TenantId",
                table: "coordination_channels",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_coordination_channels_TenantId_ProjectId",
                table: "coordination_channels",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_coordination_cursors_Id_TenantId",
                table: "coordination_cursors",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_coordination_cursors_ProjectId_ChannelId_PrincipalId",
                table: "coordination_cursors",
                columns: new[] { "ProjectId", "ChannelId", "PrincipalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_coordination_cursors_TenantId",
                table: "coordination_cursors",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_coordination_cursors_TenantId_ProjectId",
                table: "coordination_cursors",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_coordination_events_Id_TenantId",
                table: "coordination_events",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_coordination_events_ProjectId_CampaignId_EventKey",
                table: "coordination_events",
                columns: new[] { "ProjectId", "CampaignId", "EventKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_coordination_events_TenantId",
                table: "coordination_events",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_coordination_events_TenantId_ProjectId",
                table: "coordination_events",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_coordination_messages_Id_TenantId",
                table: "coordination_messages",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_coordination_messages_ProjectId_ChannelId_Sequence",
                table: "coordination_messages",
                columns: new[] { "ProjectId", "ChannelId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_coordination_messages_TenantId",
                table: "coordination_messages",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_coordination_messages_TenantId_ProjectId",
                table: "coordination_messages",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_entitlement_rules_Id_TenantId",
                table: "entitlement_rules",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_entitlement_rules_TenantId",
                table: "entitlement_rules",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_entitlement_rules_TenantId_PlanId_Key",
                table: "entitlement_rules",
                columns: new[] { "TenantId", "PlanId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_execution_campaigns_Id_TenantId",
                table: "execution_campaigns",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_execution_campaigns_TenantId",
                table: "execution_campaigns",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_execution_campaigns_TenantId_ProjectId",
                table: "execution_campaigns",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_export_jobs_Id_TenantId",
                table: "export_jobs",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_export_jobs_TenantId",
                table: "export_jobs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_external_references_Id_TenantId",
                table: "external_references",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_external_references_ProjectId_Provider_ExternalId",
                table: "external_references",
                columns: new[] { "ProjectId", "Provider", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_external_references_TenantId",
                table: "external_references",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_external_references_TenantId_ProjectId",
                table: "external_references",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_handoff_requests_Id_TenantId",
                table: "handoff_requests",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_handoff_requests_TenantId",
                table: "handoff_requests",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_handoff_requests_TenantId_ProjectId",
                table: "handoff_requests",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_import_jobs_Id_TenantId",
                table: "import_jobs",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_import_jobs_TenantId",
                table: "import_jobs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ingress_receipts_Id_TenantId",
                table: "ingress_receipts",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ingress_receipts_TenantId",
                table: "ingress_receipts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ingress_receipts_TenantId_ConnectionId_ProviderEventId",
                table: "ingress_receipts",
                columns: new[] { "TenantId", "ConnectionId", "ProviderEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_integration_connections_Id_TenantId",
                table: "integration_connections",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_integration_connections_TenantId",
                table: "integration_connections",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_integration_connections_TenantId_Provider_ExternalAccountId",
                table: "integration_connections",
                columns: new[] { "TenantId", "Provider", "ExternalAccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_integration_queue_items_Id_TenantId",
                table: "integration_queue_items",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_integration_queue_items_TenantId",
                table: "integration_queue_items",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_integration_queue_items_TenantId_ProjectId",
                table: "integration_queue_items",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_lifecycle_transitions_Id_TenantId",
                table: "lifecycle_transitions",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lifecycle_transitions_TenantId",
                table: "lifecycle_transitions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_lifecycle_transitions_TenantId_ProjectId",
                table: "lifecycle_transitions",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_managed_runners_Id_TenantId",
                table: "managed_runners",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_managed_runners_TenantId",
                table: "managed_runners",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_managed_runners_TenantId_ProjectId",
                table: "managed_runners",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_mcp_service_configs_Id_TenantId",
                table: "mcp_service_configs",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mcp_service_configs_ProjectId",
                table: "mcp_service_configs",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mcp_service_configs_TenantId",
                table: "mcp_service_configs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_mcp_service_configs_TenantId_ProjectId",
                table: "mcp_service_configs",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_metric_snapshots_Id_TenantId",
                table: "metric_snapshots",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_metric_snapshots_ProjectId_MetricKey_DefinitionVersion_Calc~",
                table: "metric_snapshots",
                columns: new[] { "ProjectId", "MetricKey", "DefinitionVersion", "CalculatedAt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_metric_snapshots_TenantId",
                table: "metric_snapshots",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_metric_snapshots_TenantId_ProjectId",
                table: "metric_snapshots",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_milestones_Id_TenantId",
                table: "milestones",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_milestones_ProjectId_Key",
                table: "milestones",
                columns: new[] { "ProjectId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_milestones_TenantId",
                table: "milestones",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_milestones_TenantId_ProjectId",
                table: "milestones",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_receipts_Id_TenantId",
                table: "notification_receipts",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_receipts_TenantId",
                table: "notification_receipts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_Id_TenantId",
                table: "outbox_messages",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_TenantId",
                table: "outbox_messages",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_TenantId_EventKey",
                table: "outbox_messages",
                columns: new[] { "TenantId", "EventKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_participation_offers_Id_TenantId",
                table: "participation_offers",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_participation_offers_TenantId",
                table: "participation_offers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_participation_offers_TenantId_ProjectId",
                table: "participation_offers",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_plan_baselines_Id_TenantId",
                table: "plan_baselines",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_plan_baselines_ProjectId_Version",
                table: "plan_baselines",
                columns: new[] { "ProjectId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_plan_baselines_TenantId",
                table: "plan_baselines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_plan_baselines_TenantId_ProjectId",
                table: "plan_baselines",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_report_snapshots_Id_TenantId",
                table: "report_snapshots",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_report_snapshots_TenantId",
                table: "report_snapshots",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_report_snapshots_TenantId_ProjectId",
                table: "report_snapshots",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_resource_reservations_Id_TenantId",
                table: "resource_reservations",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_resource_reservations_TenantId",
                table: "resource_reservations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_resource_reservations_TenantId_ProjectId",
                table: "resource_reservations",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_retention_policies_Id_TenantId",
                table: "retention_policies",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_retention_policies_TenantId",
                table: "retention_policies",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_retention_policies_TenantId_Category",
                table: "retention_policies",
                columns: new[] { "TenantId", "Category" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_search_documents_Id_TenantId",
                table: "search_documents",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_search_documents_ProjectId_ItemId",
                table: "search_documents",
                columns: new[] { "ProjectId", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_search_documents_TenantId",
                table: "search_documents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_search_documents_TenantId_ProjectId",
                table: "search_documents",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_support_access_grants_Id_TenantId",
                table: "support_access_grants",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_support_access_grants_TenantId",
                table: "support_access_grants",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_task_leases_Id_TenantId",
                table: "task_leases",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_leases_TenantId",
                table: "task_leases",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_task_leases_TenantId_ProjectId",
                table: "task_leases",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_subscriptions_Id_TenantId",
                table: "tenant_subscriptions",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_subscriptions_TenantId",
                table: "tenant_subscriptions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_usage_records_Id_TenantId",
                table: "usage_records",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usage_records_TenantId",
                table: "usage_records",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_usage_records_TenantId_IdempotencyKey",
                table: "usage_records",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_webhook_subscriptions_Id_TenantId",
                table: "webhook_subscriptions",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_webhook_subscriptions_TenantId",
                table: "webhook_subscriptions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_work_dependencies_Id_TenantId",
                table: "work_dependencies",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_dependencies_ProjectId_WorkItemId_DependsOnId",
                table: "work_dependencies",
                columns: new[] { "ProjectId", "WorkItemId", "DependsOnId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_dependencies_TenantId",
                table: "work_dependencies",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_work_dependencies_TenantId_ProjectId",
                table: "work_dependencies",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_work_items_Id_TenantId",
                table: "work_items",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_items_ProjectId_Key",
                table: "work_items",
                columns: new[] { "ProjectId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_items_TenantId",
                table: "work_items",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_work_items_TenantId_ProjectId",
                table: "work_items",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_work_packages_Id_TenantId",
                table: "work_packages",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_packages_ProjectId_CampaignId_Key",
                table: "work_packages",
                columns: new[] { "ProjectId", "CampaignId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_packages_TenantId",
                table: "work_packages",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_work_packages_TenantId_ProjectId",
                table: "work_packages",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_workspace_references_Id_TenantId",
                table: "workspace_references",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workspace_references_TenantId",
                table: "workspace_references",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_workspace_references_TenantId_ProjectId",
                table: "workspace_references",
                columns: new[] { "TenantId", "ProjectId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_runtime_instances");

            migrationBuilder.DropTable(
                name: "audit_entries");

            migrationBuilder.DropTable(
                name: "authorization_grants");

            migrationBuilder.DropTable(
                name: "baseline_revisions");

            migrationBuilder.DropTable(
                name: "billing_reconciliations");

            migrationBuilder.DropTable(
                name: "change_set_references");

            migrationBuilder.DropTable(
                name: "codebase_targets");

            migrationBuilder.DropTable(
                name: "commercial_plans");

            migrationBuilder.DropTable(
                name: "coordination_channels");

            migrationBuilder.DropTable(
                name: "coordination_cursors");

            migrationBuilder.DropTable(
                name: "coordination_events");

            migrationBuilder.DropTable(
                name: "coordination_messages");

            migrationBuilder.DropTable(
                name: "entitlement_rules");

            migrationBuilder.DropTable(
                name: "execution_campaigns");

            migrationBuilder.DropTable(
                name: "export_jobs");

            migrationBuilder.DropTable(
                name: "external_references");

            migrationBuilder.DropTable(
                name: "handoff_requests");

            migrationBuilder.DropTable(
                name: "import_jobs");

            migrationBuilder.DropTable(
                name: "ingress_receipts");

            migrationBuilder.DropTable(
                name: "integration_connections");

            migrationBuilder.DropTable(
                name: "integration_queue_items");

            migrationBuilder.DropTable(
                name: "lifecycle_transitions");

            migrationBuilder.DropTable(
                name: "managed_runners");

            migrationBuilder.DropTable(
                name: "mcp_service_configs");

            migrationBuilder.DropTable(
                name: "metric_snapshots");

            migrationBuilder.DropTable(
                name: "milestones");

            migrationBuilder.DropTable(
                name: "notification_receipts");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "participation_offers");

            migrationBuilder.DropTable(
                name: "plan_baselines");

            migrationBuilder.DropTable(
                name: "report_snapshots");

            migrationBuilder.DropTable(
                name: "resource_reservations");

            migrationBuilder.DropTable(
                name: "retention_policies");

            migrationBuilder.DropTable(
                name: "search_documents");

            migrationBuilder.DropTable(
                name: "support_access_grants");

            migrationBuilder.DropTable(
                name: "task_leases");

            migrationBuilder.DropTable(
                name: "tenant_subscriptions");

            migrationBuilder.DropTable(
                name: "usage_records");

            migrationBuilder.DropTable(
                name: "webhook_subscriptions");

            migrationBuilder.DropTable(
                name: "work_dependencies");

            migrationBuilder.DropTable(
                name: "work_items");

            migrationBuilder.DropTable(
                name: "work_packages");

            migrationBuilder.DropTable(
                name: "workspace_references");
        }
    }
}
