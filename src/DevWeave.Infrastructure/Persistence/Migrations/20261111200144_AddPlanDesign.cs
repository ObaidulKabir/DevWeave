using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevWeave.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanDesign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_knowledge_revisions_Id_TenantId_KnowledgeItemId",
                table: "knowledge_revisions",
                columns: new[] { "Id", "TenantId", "KnowledgeItemId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_knowledge_items_Id_TenantId_ProjectId",
                table: "knowledge_items",
                columns: new[] { "Id", "TenantId", "ProjectId" });

            migrationBuilder.CreateTable(
                name: "plan_expansion_proposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TypeId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Rationale = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ProposedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ResolvedItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_expansion_proposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_plan_expansion_proposals_knowledge_items_ParentItemId_Tenan~",
                        columns: x => new { x.ParentItemId, x.TenantId, x.ProjectId },
                        principalTable: "knowledge_items",
                        principalColumns: new[] { "Id", "TenantId", "ProjectId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plan_outline_nodes",
                columns: table => new
                {
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    RootItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_outline_nodes", x => x.ItemId);
                    table.UniqueConstraint("AK_plan_outline_nodes_ItemId_TenantId_ProjectId", x => new { x.ItemId, x.TenantId, x.ProjectId });
                    table.ForeignKey(
                        name: "FK_plan_outline_nodes_knowledge_items_ItemId_TenantId_ProjectId",
                        columns: x => new { x.ItemId, x.TenantId, x.ProjectId },
                        principalTable: "knowledge_items",
                        principalColumns: new[] { "Id", "TenantId", "ProjectId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_plan_outline_nodes_knowledge_items_RootItemId_TenantId_Proj~",
                        columns: x => new { x.RootItemId, x.TenantId, x.ProjectId },
                        principalTable: "knowledge_items",
                        principalColumns: new[] { "Id", "TenantId", "ProjectId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_plan_outline_nodes_plan_outline_nodes_ParentItemId_TenantId~",
                        columns: x => new { x.ParentItemId, x.TenantId, x.ProjectId },
                        principalTable: "plan_outline_nodes",
                        principalColumns: new[] { "ItemId", "TenantId", "ProjectId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plan_work_links",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    KnowledgeItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_work_links", x => x.Id);
                    table.ForeignKey(
                        name: "FK_plan_work_links_knowledge_items_KnowledgeItemId_TenantId_Pr~",
                        columns: x => new { x.KnowledgeItemId, x.TenantId, x.ProjectId },
                        principalTable: "knowledge_items",
                        principalColumns: new[] { "Id", "TenantId", "ProjectId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_plan_work_links_knowledge_revisions_RevisionId_TenantId_Kno~",
                        columns: x => new { x.RevisionId, x.TenantId, x.KnowledgeItemId },
                        principalTable: "knowledge_revisions",
                        principalColumns: new[] { "Id", "TenantId", "KnowledgeItemId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_plan_work_links_work_items_WorkItemId_TenantId_ProjectId",
                        columns: x => new { x.WorkItemId, x.TenantId, x.ProjectId },
                        principalTable: "work_items",
                        principalColumns: new[] { "Id", "TenantId", "ProjectId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_plan_expansion_proposals_ParentItemId_TenantId_ProjectId",
                table: "plan_expansion_proposals",
                columns: new[] { "ParentItemId", "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_plan_expansion_proposals_TenantId_ProjectId_Status",
                table: "plan_expansion_proposals",
                columns: new[] { "TenantId", "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_plan_outline_nodes_ParentItemId_TenantId_ProjectId",
                table: "plan_outline_nodes",
                columns: new[] { "ParentItemId", "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_plan_outline_nodes_RootItemId_TenantId_ProjectId",
                table: "plan_outline_nodes",
                columns: new[] { "RootItemId", "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_plan_outline_nodes_TenantId_ProjectId",
                table: "plan_outline_nodes",
                columns: new[] { "TenantId", "ProjectId" },
                unique: true,
                filter: "\"ParentItemId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_plan_outline_nodes_TenantId_ProjectId_RootItemId_ParentItem~",
                table: "plan_outline_nodes",
                columns: new[] { "TenantId", "ProjectId", "RootItemId", "ParentItemId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_plan_work_links_KnowledgeItemId_TenantId_ProjectId",
                table: "plan_work_links",
                columns: new[] { "KnowledgeItemId", "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_plan_work_links_RevisionId_TenantId_KnowledgeItemId",
                table: "plan_work_links",
                columns: new[] { "RevisionId", "TenantId", "KnowledgeItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_plan_work_links_TenantId_ProjectId_WorkItemId_RevisionId",
                table: "plan_work_links",
                columns: new[] { "TenantId", "ProjectId", "WorkItemId", "RevisionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_plan_work_links_WorkItemId_TenantId_ProjectId",
                table: "plan_work_links",
                columns: new[] { "WorkItemId", "TenantId", "ProjectId" });

            migrationBuilder.Sql("""
                DO $$
                DECLARE target text;
                BEGIN
                    FOREACH target IN ARRAY ARRAY[
                        'plan_outline_nodes', 'plan_work_links', 'plan_expansion_proposals'
                    ]
                    LOOP
                        EXECUTE format('ALTER TABLE public.%I OWNER TO devweave_migrator', target);
                        EXECUTE format('ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY', target);
                        EXECUTE format(
                            'CREATE POLICY %I ON public.%I FOR ALL TO devweave_app '
                            || 'USING ("TenantId" = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid) '
                            || 'WITH CHECK ("TenantId" = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid)',
                            target || '_tenant_isolation', target);
                    END LOOP;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "plan_expansion_proposals");

            migrationBuilder.DropTable(
                name: "plan_outline_nodes");

            migrationBuilder.DropTable(
                name: "plan_work_links");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_knowledge_revisions_Id_TenantId_KnowledgeItemId",
                table: "knowledge_revisions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_knowledge_items_Id_TenantId_ProjectId",
                table: "knowledge_items");
        }
    }
}
