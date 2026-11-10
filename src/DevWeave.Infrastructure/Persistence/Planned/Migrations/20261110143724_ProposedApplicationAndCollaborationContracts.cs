using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevWeave.Infrastructure.Persistence.Planned.Migrations
{
    /// <inheritdoc />
    public partial class ProposedApplicationAndCollaborationContracts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "annotations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Classification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ContentRef = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_annotations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "application_catalogue",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CapabilityManifestRef = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Version = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_catalogue", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "backup_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ManifestRef = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RequestedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backup_jobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "feature_flags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Variant = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feature_flags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "notification_preferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeliveryMode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PrincipalId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_preferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "review_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewerId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_review_assignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_applications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_applications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_annotations_Id_TenantId",
                table: "annotations",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_annotations_TenantId",
                table: "annotations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_annotations_TenantId_ProjectId",
                table: "annotations",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_application_catalogue_Key_Version",
                table: "application_catalogue",
                columns: new[] { "Key", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_backup_jobs_Id_TenantId",
                table: "backup_jobs",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_backup_jobs_TenantId",
                table: "backup_jobs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_feature_flags_Id_TenantId",
                table: "feature_flags",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_feature_flags_TenantId",
                table: "feature_flags",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_feature_flags_TenantId_Key",
                table: "feature_flags",
                columns: new[] { "TenantId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_preferences_Id_TenantId",
                table: "notification_preferences",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_preferences_TenantId",
                table: "notification_preferences",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_notification_preferences_TenantId_PrincipalId_EventType",
                table: "notification_preferences",
                columns: new[] { "TenantId", "PrincipalId", "EventType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_review_assignments_Id_TenantId",
                table: "review_assignments",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_review_assignments_TenantId",
                table: "review_assignments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_review_assignments_TenantId_ProjectId",
                table: "review_assignments",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_applications_Id_TenantId",
                table: "tenant_applications",
                columns: new[] { "Id", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_applications_TenantId",
                table: "tenant_applications",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_applications_TenantId_ApplicationId",
                table: "tenant_applications",
                columns: new[] { "TenantId", "ApplicationId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "annotations");

            migrationBuilder.DropTable(
                name: "application_catalogue");

            migrationBuilder.DropTable(
                name: "backup_jobs");

            migrationBuilder.DropTable(
                name: "feature_flags");

            migrationBuilder.DropTable(
                name: "notification_preferences");

            migrationBuilder.DropTable(
                name: "review_assignments");

            migrationBuilder.DropTable(
                name: "tenant_applications");
        }
    }
}
