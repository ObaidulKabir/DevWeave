using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevWeave.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EvidencePinsAndRelationshipMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Rationale",
                table: "relationships",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RevokedAt",
                table: "relationships",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceReference",
                table: "relationships",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ValidFrom",
                table: "relationships",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ValidTo",
                table: "relationships",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CriterionItemId",
                table: "evidence",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CriterionRevisionId",
                table: "evidence",
                type: "uuid",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "relation_types",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                columns: new[] { "AllowedSourceTypeIds", "AllowedTargetTypeIds" },
                values: new object[] { new[] { "architecture.decision", "architecture.component", "architecture.interface", "specification.technical", "design.decision", "design.component", "plan.design", "plan.decision", "plan.specification", "plan.architecture" }, new[] { "requirements.functional", "requirements.nonfunctional", "requirements.constraint", "plan.requirement", "plan.constraint", "plan.acceptance", "plan.criterion" } });

            migrationBuilder.UpdateData(
                table: "relation_types",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                columns: new[] { "AllowedSourceTypeIds", "AllowedTargetTypeIds" },
                values: new object[] { new[] { "architecture.component", "architecture.interface", "delivery.release", "delivery.deployment", "plan.implementation", "plan.component", "plan.module" }, new[] { "requirements.functional", "requirements.nonfunctional", "requirements.constraint", "specification.technical", "plan.requirement", "plan.constraint", "plan.specification" } });

            migrationBuilder.UpdateData(
                table: "relation_types",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                columns: new[] { "AllowedSourceTypeIds", "AllowedTargetTypeIds" },
                values: new object[] { new[] { "learning.lesson", "experience.feedback", "plan.test", "plan.test-case", "plan.verification", "plan.evidence", "verification.test-case" }, new[] { "requirements.functional", "requirements.nonfunctional", "requirements.constraint", "plan.requirement", "plan.constraint", "plan.acceptance", "plan.criterion" } });

            migrationBuilder.CreateIndex(
                name: "IX_evidence_TenantId_CriterionItemId_CriterionRevisionId",
                table: "evidence",
                columns: new[] { "TenantId", "CriterionItemId", "CriterionRevisionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_evidence_TenantId_CriterionItemId_CriterionRevisionId",
                table: "evidence");

            migrationBuilder.DropColumn(
                name: "Rationale",
                table: "relationships");

            migrationBuilder.DropColumn(
                name: "RevokedAt",
                table: "relationships");

            migrationBuilder.DropColumn(
                name: "SourceReference",
                table: "relationships");

            migrationBuilder.DropColumn(
                name: "ValidFrom",
                table: "relationships");

            migrationBuilder.DropColumn(
                name: "ValidTo",
                table: "relationships");

            migrationBuilder.DropColumn(
                name: "CriterionItemId",
                table: "evidence");

            migrationBuilder.DropColumn(
                name: "CriterionRevisionId",
                table: "evidence");

            migrationBuilder.UpdateData(
                table: "relation_types",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                columns: new[] { "AllowedSourceTypeIds", "AllowedTargetTypeIds" },
                values: new object[] { new string[0], new string[0] });

            migrationBuilder.UpdateData(
                table: "relation_types",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                columns: new[] { "AllowedSourceTypeIds", "AllowedTargetTypeIds" },
                values: new object[] { new string[0], new string[0] });

            migrationBuilder.UpdateData(
                table: "relation_types",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                columns: new[] { "AllowedSourceTypeIds", "AllowedTargetTypeIds" },
                values: new object[] { new string[0], new string[0] });
        }
    }
}
