using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevWeave.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkPackageScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid[]>(
                name: "ScopeRevisionIds",
                table: "work_packages",
                type: "uuid[]",
                nullable: false,
                defaultValue: new Guid[0]);

            migrationBuilder.AddColumn<string[]>(
                name: "ScopeSpecTopics",
                table: "work_packages",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<Guid[]>(
                name: "ScopeWorkItemIds",
                table: "work_packages",
                type: "uuid[]",
                nullable: false,
                defaultValue: new Guid[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScopeRevisionIds",
                table: "work_packages");

            migrationBuilder.DropColumn(
                name: "ScopeSpecTopics",
                table: "work_packages");

            migrationBuilder.DropColumn(
                name: "ScopeWorkItemIds",
                table: "work_packages");
        }
    }
}
