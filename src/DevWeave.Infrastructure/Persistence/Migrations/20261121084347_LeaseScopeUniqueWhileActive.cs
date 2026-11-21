using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevWeave.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LeaseScopeUniqueWhileActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_task_leases_WorkPackageId_ResourceScope",
                table: "task_leases");

            migrationBuilder.CreateIndex(
                name: "IX_task_leases_WorkPackageId_ResourceScope",
                table: "task_leases",
                columns: new[] { "WorkPackageId", "ResourceScope" },
                unique: true,
                filter: "\"Status\" = 'Active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_task_leases_WorkPackageId_ResourceScope",
                table: "task_leases");

            migrationBuilder.CreateIndex(
                name: "IX_task_leases_WorkPackageId_ResourceScope",
                table: "task_leases",
                columns: new[] { "WorkPackageId", "ResourceScope" },
                unique: true);
        }
    }
}
