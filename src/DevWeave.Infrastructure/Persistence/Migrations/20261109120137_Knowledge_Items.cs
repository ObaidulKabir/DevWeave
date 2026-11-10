using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevWeave.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Knowledge_Items : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "knowledge_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Classification = table.Column<int>(type: "integer", nullable: false),
                    RepresentationKind = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    ReviewState = table.Column<int>(type: "integer", nullable: false),
                    VerificationState = table.Column<int>(type: "integer", nullable: false),
                    CurrencyState = table.Column<int>(type: "integer", nullable: false),
                    TypeId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CurrentRevisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentRevisionNumber = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_knowledge_items_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_items_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_revisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    KnowledgeItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionNumber = table.Column<int>(type: "integer", nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Content = table.Column<string>(type: "jsonb", nullable: false),
                    Actor = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SourceReference = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SupersededRevisionId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_revisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_knowledge_revisions_knowledge_items_KnowledgeItemId",
                        column: x => x.KnowledgeItemId,
                        principalTable: "knowledge_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_revisions_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_items_ProjectId",
                table: "knowledge_items",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_items_TenantId_ProjectId_Key",
                table: "knowledge_items",
                columns: new[] { "TenantId", "ProjectId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_revisions_KnowledgeItemId",
                table: "knowledge_revisions",
                column: "KnowledgeItemId");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_revisions_TenantId_KnowledgeItemId_RevisionNumber",
                table: "knowledge_revisions",
                columns: new[] { "TenantId", "KnowledgeItemId", "RevisionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "knowledge_revisions");

            migrationBuilder.DropTable(
                name: "knowledge_items");
        }
    }
}
