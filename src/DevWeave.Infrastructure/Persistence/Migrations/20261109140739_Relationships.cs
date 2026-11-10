using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DevWeave.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Relationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "relation_types",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AllowedSourceTypeIds = table.Column<string[]>(type: "text[]", nullable: false),
                    AllowedTargetTypeIds = table.Column<string[]>(type: "text[]", nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    Cardinality = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_relation_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "relationships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelationTypeCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_relationships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_relationships_knowledge_items_SourceItemId",
                        column: x => x.SourceItemId,
                        principalTable: "knowledge_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_relationships_knowledge_items_TargetItemId",
                        column: x => x.TargetItemId,
                        principalTable: "knowledge_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_relationships_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_relationships_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "relation_types",
                columns: new[] { "Id", "AllowedSourceTypeIds", "AllowedTargetTypeIds", "Cardinality", "Code", "Direction", "Name", "Status" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000001"), new string[0], new string[0], 1, "contains", 1, "Contains", 1 },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new string[0], new string[0], 1, "depends_on", 1, "Depends On", 1 },
                    { new Guid("00000000-0000-0000-0000-000000000003"), new string[0], new string[0], 1, "satisfies", 1, "Satisfies", 1 },
                    { new Guid("00000000-0000-0000-0000-000000000004"), new string[0], new string[0], 1, "implements", 1, "Implements", 1 },
                    { new Guid("00000000-0000-0000-0000-000000000005"), new string[0], new string[0], 1, "validates", 1, "Validates", 1 },
                    { new Guid("00000000-0000-0000-0000-000000000006"), new string[0], new string[0], 1, "derived_from", 1, "Derived From", 1 },
                    { new Guid("00000000-0000-0000-0000-000000000007"), new string[0], new string[0], 1, "generated_by", 1, "Generated By", 1 },
                    { new Guid("00000000-0000-0000-0000-000000000008"), new string[0], new string[0], 1, "approved_by", 1, "Approved By", 1 },
                    { new Guid("00000000-0000-0000-0000-000000000009"), new string[0], new string[0], 1, "supersedes", 1, "Supersedes", 1 },
                    { new Guid("00000000-0000-0000-0000-00000000000a"), new string[0], new string[0], 1, "references", 1, "References", 1 },
                    { new Guid("00000000-0000-0000-0000-00000000000b"), new string[0], new string[0], 1, "conflicts_with", 1, "Conflicts With", 1 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_relation_types_Code",
                table: "relation_types",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_relationships_ProjectId",
                table: "relationships",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_relationships_SourceItemId",
                table: "relationships",
                column: "SourceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_relationships_TargetItemId",
                table: "relationships",
                column: "TargetItemId");

            migrationBuilder.CreateIndex(
                name: "IX_relationships_TenantId_ProjectId",
                table: "relationships",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_relationships_TenantId_SourceItemId",
                table: "relationships",
                columns: new[] { "TenantId", "SourceItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_relationships_TenantId_TargetItemId",
                table: "relationships",
                columns: new[] { "TenantId", "TargetItemId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "relation_types");

            migrationBuilder.DropTable(
                name: "relationships");
        }
    }
}
