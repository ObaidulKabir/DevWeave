using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevWeave.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecordRuntimeDeclaredCapabilities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "Capabilities",
                table: "agent_runtime_instances",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Capabilities",
                table: "agent_runtime_instances");
        }
    }
}
