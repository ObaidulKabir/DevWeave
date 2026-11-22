using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevWeave.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class KnowledgeRevisionValidTimeAndAgentExecutionMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ValidFrom",
                table: "knowledge_revisions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ValidTo",
                table: "knowledge_revisions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "agent_execution_metadata",
                columns: table => new
                {
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ModelVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ConfigurationFingerprint = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ToolFingerprint = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExternalRunReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AuthorizationContext = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_execution_metadata", x => x.ActivityId);
                    table.ForeignKey(
                        name: "FK_agent_execution_metadata_activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_agent_execution_metadata_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_execution_metadata_TenantId",
                table: "agent_execution_metadata",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_execution_metadata");

            migrationBuilder.DropColumn(
                name: "ValidFrom",
                table: "knowledge_revisions");

            migrationBuilder.DropColumn(
                name: "ValidTo",
                table: "knowledge_revisions");
        }
    }
}
