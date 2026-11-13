using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevWeave.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUniquePlanBaselineVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_plan_baselines_TenantId_ProjectId_ExpectedPlanVersion",
                table: "plan_baselines",
                columns: new[] { "TenantId", "ProjectId", "ExpectedPlanVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_plan_baselines_TenantId_ProjectId_ExpectedPlanVersion",
                table: "plan_baselines");
        }
    }
}
