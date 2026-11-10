using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevWeave.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Artifacts_Evidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "artifacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    MediaType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SizeInBytes = table.Column<long>(type: "bigint", nullable: false),
                    Checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ChecksumAlgorithm = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Classification = table.Column<int>(type: "integer", nullable: false),
                    Creator = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artifacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_artifacts_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_artifacts_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IntegrityHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RelatedArtifactId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedCriterion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ReviewState = table.Column<int>(type: "integer", nullable: false),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_evidence_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_evidence_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_artifacts_ProjectId",
                table: "artifacts",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_artifacts_TenantId_ProjectId",
                table: "artifacts",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_artifacts_TenantId_ProjectId_Key_Version",
                table: "artifacts",
                columns: new[] { "TenantId", "ProjectId", "Key", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_evidence_ProjectId",
                table: "evidence",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_evidence_TenantId_OriginActivityId",
                table: "evidence",
                columns: new[] { "TenantId", "OriginActivityId" });

            migrationBuilder.CreateIndex(
                name: "IX_evidence_TenantId_ProjectId",
                table: "evidence",
                columns: new[] { "TenantId", "ProjectId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "artifacts");

            migrationBuilder.DropTable(
                name: "evidence");
        }
    }
}
