using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevWeave.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkthroughKindAndBlockedReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BlockedReason",
                table: "work_package_workthroughs",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "work_package_workthroughs",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BlockedReason",
                table: "work_package_workthroughs");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "work_package_workthroughs");
        }
    }
}
