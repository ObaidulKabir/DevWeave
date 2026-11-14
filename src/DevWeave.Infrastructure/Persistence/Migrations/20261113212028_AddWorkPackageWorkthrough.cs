using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevWeave.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkPackageWorkthrough : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "work_package_workthroughs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkPackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    WhatSucceeded = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Steps = table.Column<string[]>(type: "text[]", nullable: false),
                    ChangeSetId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_package_workthroughs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_work_package_workthroughs_TenantId_WorkPackageId_RecordedAt",
                table: "work_package_workthroughs",
                columns: new[] { "TenantId", "WorkPackageId", "RecordedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "work_package_workthroughs");
        }
    }
}
