using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevWeave.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MilestoneTransitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "milestones",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "milestones",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "milestone_transitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    MilestoneId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromState = table.Column<int>(type: "integer", nullable: false),
                    ToState = table.Column<int>(type: "integer", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_milestone_transitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_milestone_transitions_milestones_MilestoneId_ProjectId_Tena~",
                        columns: x => new { x.MilestoneId, x.ProjectId, x.TenantId },
                        principalTable: "milestones",
                        principalColumns: new[] { "Id", "ProjectId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_milestone_transitions_MilestoneId_ProjectId_TenantId",
                table: "milestone_transitions",
                columns: new[] { "MilestoneId", "ProjectId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_milestone_transitions_TenantId_ProjectId_MilestoneId_Version",
                table: "milestone_transitions",
                columns: new[] { "TenantId", "ProjectId", "MilestoneId", "Version" },
                unique: true);

            migrationBuilder.Sql("""
                GRANT SELECT, INSERT ON public.milestone_transitions TO devweave_app;
                REVOKE UPDATE, DELETE ON public.milestone_transitions FROM devweave_app;
                ALTER TABLE public.milestone_transitions ENABLE ROW LEVEL SECURITY;
                CREATE POLICY milestone_transitions_tenant_isolation
                    ON public.milestone_transitions AS PERMISSIVE FOR ALL TO devweave_app
                    USING ("TenantId" = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                    WITH CHECK ("TenantId" = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP POLICY IF EXISTS milestone_transitions_tenant_isolation ON public.milestone_transitions;
                ALTER TABLE public.milestone_transitions DISABLE ROW LEVEL SECURITY;
                """);
            migrationBuilder.DropTable(
                name: "milestone_transitions");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "milestones");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "milestones");
        }
    }
}
