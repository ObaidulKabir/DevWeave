using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevWeave.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LifecycleWorkPlanning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_projects_Id_TenantId",
                table: "projects",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.CreateTable(
                name: "milestones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    State = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_milestones", x => x.Id);
                    table.UniqueConstraint("AK_milestones_Id_ProjectId_TenantId", x => new { x.Id, x.ProjectId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_milestones_projects_ProjectId_TenantId",
                        columns: x => new { x.ProjectId, x.TenantId },
                        principalTable: "projects",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "work_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    MilestoneId = table.Column<Guid>(type: "uuid", nullable: true),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    PlannedState = table.Column<int>(type: "integer", nullable: false),
                    ReportedState = table.Column<int>(type: "integer", nullable: false),
                    VerificationState = table.Column<int>(type: "integer", nullable: false),
                    ApprovalState = table.Column<int>(type: "integer", nullable: false),
                    ReportedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VerifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_items", x => x.Id);
                    table.UniqueConstraint("AK_work_items_Id_TenantId_ProjectId", x => new { x.Id, x.TenantId, x.ProjectId });
                    table.ForeignKey(
                        name: "FK_work_items_milestones_MilestoneId_ProjectId_TenantId",
                        columns: x => new { x.MilestoneId, x.ProjectId, x.TenantId },
                        principalTable: "milestones",
                        principalColumns: new[] { "Id", "ProjectId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_work_items_projects_ProjectId_TenantId",
                        columns: x => new { x.ProjectId, x.TenantId },
                        principalTable: "projects",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "work_item_dependencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    DependsOnId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_item_dependencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_work_item_dependencies_work_items_DependsOnId_TenantId_Proj~",
                        columns: x => new { x.DependsOnId, x.TenantId, x.ProjectId },
                        principalTable: "work_items",
                        principalColumns: new[] { "Id", "TenantId", "ProjectId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_work_item_dependencies_work_items_WorkItemId_TenantId_Proje~",
                        columns: x => new { x.WorkItemId, x.TenantId, x.ProjectId },
                        principalTable: "work_items",
                        principalColumns: new[] { "Id", "TenantId", "ProjectId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "work_item_transitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Dimension = table.Column<int>(type: "integer", nullable: false),
                    FromState = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ToState = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_item_transitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_work_item_transitions_work_items_WorkItemId_TenantId_Projec~",
                        columns: x => new { x.WorkItemId, x.TenantId, x.ProjectId },
                        principalTable: "work_items",
                        principalColumns: new[] { "Id", "TenantId", "ProjectId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_milestones_ProjectId_TenantId",
                table: "milestones",
                columns: new[] { "ProjectId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_milestones_TenantId_ProjectId_Key",
                table: "milestones",
                columns: new[] { "TenantId", "ProjectId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_item_dependencies_DependsOnId_TenantId_ProjectId",
                table: "work_item_dependencies",
                columns: new[] { "DependsOnId", "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_work_item_dependencies_TenantId_ProjectId_WorkItemId_Depend~",
                table: "work_item_dependencies",
                columns: new[] { "TenantId", "ProjectId", "WorkItemId", "DependsOnId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_item_dependencies_WorkItemId_TenantId_ProjectId",
                table: "work_item_dependencies",
                columns: new[] { "WorkItemId", "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_work_item_transitions_TenantId_ProjectId_WorkItemId_Version",
                table: "work_item_transitions",
                columns: new[] { "TenantId", "ProjectId", "WorkItemId", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_work_item_transitions_WorkItemId_TenantId_ProjectId",
                table: "work_item_transitions",
                columns: new[] { "WorkItemId", "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_work_items_MilestoneId_ProjectId_TenantId",
                table: "work_items",
                columns: new[] { "MilestoneId", "ProjectId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_work_items_ProjectId_TenantId",
                table: "work_items",
                columns: new[] { "ProjectId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_work_items_TenantId_ProjectId_Key",
                table: "work_items",
                columns: new[] { "TenantId", "ProjectId", "Key" },
                unique: true);

            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE ON public.work_items, public.milestones TO devweave_app;
                GRANT SELECT, INSERT ON public.work_item_dependencies, public.work_item_transitions TO devweave_app;
                REVOKE DELETE ON public.work_items, public.milestones FROM devweave_app;
                REVOKE UPDATE, DELETE ON public.work_item_dependencies, public.work_item_transitions FROM devweave_app;

                DO $$
                DECLARE target text;
                BEGIN
                    FOREACH target IN ARRAY ARRAY[
                        'work_items', 'milestones', 'work_item_dependencies', 'work_item_transitions'
                    ]
                    LOOP
                        EXECUTE format('ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY', target);
                        EXECUTE format(
                            'CREATE POLICY %I ON public.%I AS PERMISSIVE FOR ALL TO devweave_app '
                            || 'USING ("TenantId" = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid) '
                            || 'WITH CHECK ("TenantId" = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid)',
                            target || '_tenant_isolation', target);
                    END LOOP;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE target text;
                BEGIN
                    FOREACH target IN ARRAY ARRAY[
                        'work_items', 'milestones', 'work_item_dependencies', 'work_item_transitions'
                    ]
                    LOOP
                        EXECUTE format('DROP POLICY IF EXISTS %I ON public.%I', target || '_tenant_isolation', target);
                        EXECUTE format('ALTER TABLE public.%I DISABLE ROW LEVEL SECURITY', target);
                    END LOOP;
                END $$;
                """);

            migrationBuilder.DropTable(
                name: "work_item_dependencies");

            migrationBuilder.DropTable(
                name: "work_item_transitions");

            migrationBuilder.DropTable(
                name: "work_items");

            migrationBuilder.DropTable(
                name: "milestones");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_projects_Id_TenantId",
                table: "projects");
        }
    }
}
