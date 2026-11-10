using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevWeave.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UniqueTransitionVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_work_item_transitions_TenantId_ProjectId_WorkItemId_Version",
                table: "work_item_transitions");

            migrationBuilder.DropIndex(
                name: "IX_milestone_transitions_TenantId_ProjectId_MilestoneId_Version",
                table: "milestone_transitions");

            migrationBuilder.CreateIndex(
                name: "IX_work_item_transitions_TenantId_ProjectId_WorkItemId_Version",
                table: "work_item_transitions",
                columns: new[] { "TenantId", "ProjectId", "WorkItemId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_milestone_transitions_TenantId_ProjectId_MilestoneId_Version",
                table: "milestone_transitions",
                columns: new[] { "TenantId", "ProjectId", "MilestoneId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_work_item_transitions_TenantId_ProjectId_WorkItemId_Version",
                table: "work_item_transitions");

            migrationBuilder.DropIndex(
                name: "IX_milestone_transitions_TenantId_ProjectId_MilestoneId_Version",
                table: "milestone_transitions");

            migrationBuilder.CreateIndex(
                name: "IX_work_item_transitions_TenantId_ProjectId_WorkItemId_Version",
                table: "work_item_transitions",
                columns: new[] { "TenantId", "ProjectId", "WorkItemId", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_milestone_transitions_TenantId_ProjectId_MilestoneId_Version",
                table: "milestone_transitions",
                columns: new[] { "TenantId", "ProjectId", "MilestoneId", "Version" });
        }
    }
}
