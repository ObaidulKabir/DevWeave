using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevWeave.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase5ContextStaleContinuationAndSemanticConflicts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PredecessorWorkPackageId",
                table: "work_packages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContinuationWorkPackageId",
                table: "plan_rebase_decisions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsStale",
                table: "context_packages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StaleAt",
                table: "context_packages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StaleReason",
                table: "context_packages",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "semantic_conflicts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeftRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RightRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RecordedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    Resolution = table.Column<int>(type: "integer", nullable: true),
                    ResolvedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolutionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_semantic_conflicts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_semantic_conflicts_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_semantic_conflicts_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_work_packages_TenantId_PredecessorWorkPackageId",
                table: "work_packages",
                columns: new[] { "TenantId", "PredecessorWorkPackageId" });

            migrationBuilder.CreateIndex(
                name: "IX_context_packages_TenantId_ProjectId_IsStale_CreatedAt",
                table: "context_packages",
                columns: new[] { "TenantId", "ProjectId", "IsStale", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_context_packages_TenantId_ProjectId_TaskKey_IsStale_Created~",
                table: "context_packages",
                columns: new[] { "TenantId", "ProjectId", "TaskKey", "IsStale", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_semantic_conflicts_ProjectId",
                table: "semantic_conflicts",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_semantic_conflicts_TenantId_ProjectId_IsResolved",
                table: "semantic_conflicts",
                columns: new[] { "TenantId", "ProjectId", "IsResolved" });

            migrationBuilder.Sql("""
                ALTER TABLE public.semantic_conflicts OWNER TO devweave_migrator;
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    EXECUTE 'ALTER TABLE public.semantic_conflicts ENABLE ROW LEVEL SECURITY';
                    EXECUTE 'DROP POLICY IF EXISTS semantic_conflicts_tenant_isolation ON public.semantic_conflicts';
                    EXECUTE
                        'CREATE POLICY semantic_conflicts_tenant_isolation ON public.semantic_conflicts '
                        || 'AS PERMISSIVE FOR ALL TO devweave_app '
                        || 'USING ("TenantId" = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid) '
                        || 'WITH CHECK ("TenantId" = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid)';
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP POLICY IF EXISTS semantic_conflicts_tenant_isolation ON public.semantic_conflicts;
                """);

            migrationBuilder.DropTable(
                name: "semantic_conflicts");

            migrationBuilder.DropIndex(
                name: "IX_work_packages_TenantId_PredecessorWorkPackageId",
                table: "work_packages");

            migrationBuilder.DropIndex(
                name: "IX_context_packages_TenantId_ProjectId_IsStale_CreatedAt",
                table: "context_packages");

            migrationBuilder.DropIndex(
                name: "IX_context_packages_TenantId_ProjectId_TaskKey_IsStale_Created~",
                table: "context_packages");

            migrationBuilder.DropColumn(
                name: "PredecessorWorkPackageId",
                table: "work_packages");

            migrationBuilder.DropColumn(
                name: "ContinuationWorkPackageId",
                table: "plan_rebase_decisions");

            migrationBuilder.DropColumn(
                name: "IsStale",
                table: "context_packages");

            migrationBuilder.DropColumn(
                name: "StaleAt",
                table: "context_packages");

            migrationBuilder.DropColumn(
                name: "StaleReason",
                table: "context_packages");
        }
    }
}
