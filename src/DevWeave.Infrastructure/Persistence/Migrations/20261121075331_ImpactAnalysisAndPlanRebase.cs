using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevWeave.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ImpactAnalysisAndPlanRebase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "impact_analysis_reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangeSetId = table.Column<Guid>(type: "uuid", nullable: false),
                    RulesVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PolicyVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AnalyzedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AnalyzedBy = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_impact_analysis_reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_impact_analysis_reports_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_impact_analysis_reports_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plan_rebase_decisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkPackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImpactReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    AuthorizedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorizedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_rebase_decisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_plan_rebase_decisions_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_plan_rebase_decisions_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "requirement_change_sets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_requirement_change_sets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_requirement_change_sets_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_requirement_change_sets_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "impact_analysis_findings",
                columns: table => new
                {
                    Position = table.Column<int>(type: "integer", nullable: false),
                    report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResourceKind = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Disposition = table.Column<int>(type: "integer", nullable: false),
                    RelationPath = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Rule = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_impact_analysis_findings", x => new { x.report_id, x.Position });
                    table.ForeignKey(
                        name: "FK_impact_analysis_findings_impact_analysis_reports_report_id",
                        column: x => x.report_id,
                        principalTable: "impact_analysis_reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "requirement_change_entries",
                columns: table => new
                {
                    Position = table.Column<int>(type: "integer", nullable: false),
                    change_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    PreviousRevisionIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    ResultRevisionIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ValidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_requirement_change_entries", x => new { x.change_set_id, x.Position });
                    table.ForeignKey(
                        name: "FK_requirement_change_entries_requirement_change_sets_change_s~",
                        column: x => x.change_set_id,
                        principalTable: "requirement_change_sets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_impact_analysis_reports_ProjectId",
                table: "impact_analysis_reports",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_impact_analysis_reports_TenantId_ChangeSetId",
                table: "impact_analysis_reports",
                columns: new[] { "TenantId", "ChangeSetId" });

            migrationBuilder.CreateIndex(
                name: "IX_impact_analysis_reports_TenantId_ProjectId",
                table: "impact_analysis_reports",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_plan_rebase_decisions_ProjectId",
                table: "plan_rebase_decisions",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_plan_rebase_decisions_TenantId_ImpactReportId",
                table: "plan_rebase_decisions",
                columns: new[] { "TenantId", "ImpactReportId" });

            migrationBuilder.CreateIndex(
                name: "IX_plan_rebase_decisions_TenantId_WorkPackageId_AuthorizedAt",
                table: "plan_rebase_decisions",
                columns: new[] { "TenantId", "WorkPackageId", "AuthorizedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_requirement_change_sets_ProjectId",
                table: "requirement_change_sets",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_requirement_change_sets_TenantId_ProjectId",
                table: "requirement_change_sets",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.Sql("""
                ALTER TABLE public.requirement_change_sets OWNER TO devweave_migrator;
                ALTER TABLE public.requirement_change_entries OWNER TO devweave_migrator;
                ALTER TABLE public.impact_analysis_reports OWNER TO devweave_migrator;
                ALTER TABLE public.impact_analysis_findings OWNER TO devweave_migrator;
                ALTER TABLE public.plan_rebase_decisions OWNER TO devweave_migrator;
                """);

            migrationBuilder.Sql("""
                DO $$
                DECLARE target text;
                BEGIN
                    FOREACH target IN ARRAY ARRAY[
                        'requirement_change_sets', 'impact_analysis_reports', 'plan_rebase_decisions'
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
                            ('requirement_change_entries', 'requirement_change_sets', 'change_set_id'),
                            ('impact_analysis_findings', 'impact_analysis_reports', 'report_id')
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
                DROP POLICY IF EXISTS requirement_change_sets_tenant_isolation ON public.requirement_change_sets;
                DROP POLICY IF EXISTS requirement_change_entries_tenant_isolation ON public.requirement_change_entries;
                DROP POLICY IF EXISTS impact_analysis_reports_tenant_isolation ON public.impact_analysis_reports;
                DROP POLICY IF EXISTS impact_analysis_findings_tenant_isolation ON public.impact_analysis_findings;
                DROP POLICY IF EXISTS plan_rebase_decisions_tenant_isolation ON public.plan_rebase_decisions;
                """);

            migrationBuilder.DropTable(
                name: "impact_analysis_findings");

            migrationBuilder.DropTable(
                name: "plan_rebase_decisions");

            migrationBuilder.DropTable(
                name: "requirement_change_entries");

            migrationBuilder.DropTable(
                name: "impact_analysis_reports");

            migrationBuilder.DropTable(
                name: "requirement_change_sets");
        }
    }
}
