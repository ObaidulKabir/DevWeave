using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevWeave.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecordExecutionLocationEntitlementDecisionsAndTrainingPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExecutionLocation",
                table: "work_packages",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "CustomerManaged");

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "work_packages",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "customer");

            migrationBuilder.AddColumn<string>(
                name: "BillingMeter",
                table: "agent_runtime_instances",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExecutionLocation",
                table: "agent_runtime_instances",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "CustomerManaged");

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "agent_runtime_instances",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "customer");

            migrationBuilder.AddColumn<string>(
                name: "WorkspaceProvider",
                table: "agent_runtime_instances",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExecutionLocation",
                table: "activities",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "CustomerManaged");

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "activities",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "customer");

            migrationBuilder.CreateTable(
                name: "entitlement_decisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlanVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Meter = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Requested = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Observed = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PolicyVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entitlement_decisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_training_policies",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OptedIn = table.Column<bool>(type: "boolean", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_training_policies", x => x.TenantId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_entitlement_decisions_TenantId_DecidedAt",
                table: "entitlement_decisions",
                columns: new[] { "TenantId", "DecidedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "entitlement_decisions");

            migrationBuilder.DropTable(
                name: "tenant_training_policies");

            migrationBuilder.DropColumn(
                name: "ExecutionLocation",
                table: "work_packages");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "work_packages");

            migrationBuilder.DropColumn(
                name: "BillingMeter",
                table: "agent_runtime_instances");

            migrationBuilder.DropColumn(
                name: "ExecutionLocation",
                table: "agent_runtime_instances");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "agent_runtime_instances");

            migrationBuilder.DropColumn(
                name: "WorkspaceProvider",
                table: "agent_runtime_instances");

            migrationBuilder.DropColumn(
                name: "ExecutionLocation",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "activities");
        }
    }
}
