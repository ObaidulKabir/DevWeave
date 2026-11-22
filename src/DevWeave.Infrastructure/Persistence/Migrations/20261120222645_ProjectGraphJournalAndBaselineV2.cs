using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DevWeave.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProjectGraphJournalAndBaselineV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "plan_baseline_manifests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ManifestSchemaVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    HistoricalPrecision = table.Column<int>(type: "integer", nullable: false),
                    LegacyAcceptanceCriteria = table.Column<string[]>(type: "text[]", nullable: false),
                    LifecycleProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    LifecycleProfileVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PolicyVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PredecessorBaselineId = table.Column<Guid>(type: "uuid", nullable: true),
                    ManifestChecksum = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_baseline_manifests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_plan_baseline_manifests_plan_baselines_Id",
                        column: x => x.Id,
                        principalTable: "plan_baselines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_plan_baseline_manifests_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_plan_baseline_manifests_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    EventType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EventSchemaVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ValidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    BaselineId = table.Column<Guid>(type: "uuid", nullable: true),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AuthorizationContext = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_events_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_events_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plan_baseline_criterion_refs",
                columns: table => new
                {
                    position = table.Column<int>(type: "integer", nullable: false),
                    baseline_id = table.Column<Guid>(type: "uuid", nullable: false),
                    knowledge_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    knowledge_revision_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_baseline_criterion_refs", x => new { x.baseline_id, x.position });
                    table.ForeignKey(
                        name: "FK_plan_baseline_criterion_refs_plan_baseline_manifests_baseli~",
                        column: x => x.baseline_id,
                        principalTable: "plan_baseline_manifests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "plan_baseline_dependency_edges",
                columns: table => new
                {
                    position = table.Column<int>(type: "integer", nullable: false),
                    baseline_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_kind = table.Column<int>(type: "integer", nullable: false),
                    from_identifier = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    to_kind = table.Column<int>(type: "integer", nullable: false),
                    to_identifier = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    relation_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_baseline_dependency_edges", x => new { x.baseline_id, x.position });
                    table.ForeignKey(
                        name: "FK_plan_baseline_dependency_edges_plan_baseline_manifests_base~",
                        column: x => x.baseline_id,
                        principalTable: "plan_baseline_manifests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "plan_baseline_revision_refs",
                columns: table => new
                {
                    position = table.Column<int>(type: "integer", nullable: false),
                    baseline_id = table.Column<Guid>(type: "uuid", nullable: false),
                    knowledge_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    knowledge_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    schema_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_baseline_revision_refs", x => new { x.baseline_id, x.position });
                    table.ForeignKey(
                        name: "FK_plan_baseline_revision_refs_plan_baseline_manifests_baselin~",
                        column: x => x.baseline_id,
                        principalTable: "plan_baseline_manifests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "plan_baseline_semantic_edges",
                columns: table => new
                {
                    position = table.Column<int>(type: "integer", nullable: false),
                    baseline_id = table.Column<Guid>(type: "uuid", nullable: false),
                    relationship_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    relation_type_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_baseline_semantic_edges", x => new { x.baseline_id, x.position });
                    table.ForeignKey(
                        name: "FK_plan_baseline_semantic_edges_plan_baseline_manifests_baseli~",
                        column: x => x.baseline_id,
                        principalTable: "plan_baseline_manifests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "plan_baseline_specification_bindings",
                columns: table => new
                {
                    position = table.Column<int>(type: "integer", nullable: false),
                    baseline_id = table.Column<Guid>(type: "uuid", nullable: false),
                    topic = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    knowledge_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    knowledge_revision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    binding_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_baseline_specification_bindings", x => new { x.baseline_id, x.position });
                    table.ForeignKey(
                        name: "FK_plan_baseline_specification_bindings_plan_baseline_manifest~",
                        column: x => x.baseline_id,
                        principalTable: "plan_baseline_manifests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_event_inputs",
                columns: table => new
                {
                    position = table.Column<int>(type: "integer", nullable: false),
                    project_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_kind = table.Column<int>(type: "integer", nullable: false),
                    resource_identifier = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    revision_identifier = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_event_inputs", x => new { x.project_event_id, x.position });
                    table.ForeignKey(
                        name: "FK_project_event_inputs_project_events_project_event_id",
                        column: x => x.project_event_id,
                        principalTable: "project_events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_event_outputs",
                columns: table => new
                {
                    position = table.Column<int>(type: "integer", nullable: false),
                    project_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_kind = table.Column<int>(type: "integer", nullable: false),
                    resource_identifier = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    revision_identifier = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_event_outputs", x => new { x.project_event_id, x.position });
                    table.ForeignKey(
                        name: "FK_project_event_outputs_project_events_project_event_id",
                        column: x => x.project_event_id,
                        principalTable: "project_events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_plan_baseline_manifests_ManifestChecksum",
                table: "plan_baseline_manifests",
                column: "ManifestChecksum");

            migrationBuilder.CreateIndex(
                name: "IX_plan_baseline_manifests_ProjectId",
                table: "plan_baseline_manifests",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_plan_baseline_manifests_TenantId_ProjectId",
                table: "plan_baseline_manifests",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_project_events_CorrelationId",
                table: "project_events",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_project_events_ProjectId",
                table: "project_events",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_project_events_TenantId_ProjectId_IdempotencyKey",
                table: "project_events",
                columns: new[] { "TenantId", "ProjectId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_events_TenantId_ProjectId_Sequence",
                table: "project_events",
                columns: new[] { "TenantId", "ProjectId", "Sequence" },
                unique: true);

            migrationBuilder.Sql("""
                ALTER TABLE public.project_events OWNER TO devweave_migrator;
                ALTER TABLE public.plan_baseline_manifests OWNER TO devweave_migrator;
                ALTER TABLE public.project_event_inputs OWNER TO devweave_migrator;
                ALTER TABLE public.project_event_outputs OWNER TO devweave_migrator;
                ALTER TABLE public.plan_baseline_revision_refs OWNER TO devweave_migrator;
                ALTER TABLE public.plan_baseline_semantic_edges OWNER TO devweave_migrator;
                ALTER TABLE public.plan_baseline_specification_bindings OWNER TO devweave_migrator;
                ALTER TABLE public.plan_baseline_criterion_refs OWNER TO devweave_migrator;
                ALTER TABLE public.plan_baseline_dependency_edges OWNER TO devweave_migrator;
                """);

            migrationBuilder.Sql("""
                DO $$
                DECLARE target text;
                BEGIN
                    FOREACH target IN ARRAY ARRAY[
                        'project_events', 'plan_baseline_manifests'
                    ]
                    LOOP
                        EXECUTE format('ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY', target);
                        EXECUTE format('DROP POLICY IF EXISTS %I ON public.%I', target || '_tenant_isolation', target);
                        EXECUTE format(
                            'CREATE POLICY %I ON public.%I AS PERMISSIVE FOR ALL TO devweave_app '
                            || 'USING ("TenantId" = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid) '
                            || 'WITH CHECK ("TenantId" = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid)',
                            target || '_tenant_isolation', target);
                    END LOOP;
                END $$;
                """);

            migrationBuilder.Sql("""
                DO $$
                DECLARE spec record;
                BEGIN
                    FOR spec IN
                        SELECT * FROM (VALUES
                            ('project_event_inputs', 'project_events', 'project_event_id'),
                            ('project_event_outputs', 'project_events', 'project_event_id'),
                            ('plan_baseline_revision_refs', 'plan_baseline_manifests', 'baseline_id'),
                            ('plan_baseline_semantic_edges', 'plan_baseline_manifests', 'baseline_id'),
                            ('plan_baseline_specification_bindings', 'plan_baseline_manifests', 'baseline_id'),
                            ('plan_baseline_criterion_refs', 'plan_baseline_manifests', 'baseline_id'),
                            ('plan_baseline_dependency_edges', 'plan_baseline_manifests', 'baseline_id')
                        ) AS t(child, parent, fk)
                    LOOP
                        EXECUTE format('ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY', spec.child);
                        EXECUTE format('DROP POLICY IF EXISTS %I ON public.%I', spec.child || '_tenant_isolation', spec.child);
                        EXECUTE format(
                            'CREATE POLICY %I ON public.%I AS PERMISSIVE FOR ALL TO devweave_app '
                            || 'USING (EXISTS (SELECT 1 FROM public.%I p WHERE p."Id" = %I.%I '
                            || 'AND p."TenantId" = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid)) '
                            || 'WITH CHECK (EXISTS (SELECT 1 FROM public.%I p WHERE p."Id" = %I.%I '
                            || 'AND p."TenantId" = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid))',
                            spec.child || '_tenant_isolation', spec.child,
                            spec.parent, spec.child, spec.fk,
                            spec.parent, spec.child, spec.fk);
                    END LOOP;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP POLICY IF EXISTS project_events_tenant_isolation ON public.project_events;
                DROP POLICY IF EXISTS plan_baseline_manifests_tenant_isolation ON public.plan_baseline_manifests;
                DROP POLICY IF EXISTS project_event_inputs_tenant_isolation ON public.project_event_inputs;
                DROP POLICY IF EXISTS project_event_outputs_tenant_isolation ON public.project_event_outputs;
                DROP POLICY IF EXISTS plan_baseline_revision_refs_tenant_isolation ON public.plan_baseline_revision_refs;
                DROP POLICY IF EXISTS plan_baseline_semantic_edges_tenant_isolation ON public.plan_baseline_semantic_edges;
                DROP POLICY IF EXISTS plan_baseline_specification_bindings_tenant_isolation ON public.plan_baseline_specification_bindings;
                DROP POLICY IF EXISTS plan_baseline_criterion_refs_tenant_isolation ON public.plan_baseline_criterion_refs;
                DROP POLICY IF EXISTS plan_baseline_dependency_edges_tenant_isolation ON public.plan_baseline_dependency_edges;
                """);

            migrationBuilder.DropTable(
                name: "plan_baseline_criterion_refs");

            migrationBuilder.DropTable(
                name: "plan_baseline_dependency_edges");

            migrationBuilder.DropTable(
                name: "plan_baseline_revision_refs");

            migrationBuilder.DropTable(
                name: "plan_baseline_semantic_edges");

            migrationBuilder.DropTable(
                name: "plan_baseline_specification_bindings");

            migrationBuilder.DropTable(
                name: "project_event_inputs");

            migrationBuilder.DropTable(
                name: "project_event_outputs");

            migrationBuilder.DropTable(
                name: "plan_baseline_manifests");

            migrationBuilder.DropTable(
                name: "project_events");
        }
    }
}
