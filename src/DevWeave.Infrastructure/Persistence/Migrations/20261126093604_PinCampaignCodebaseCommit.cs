using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevWeave.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PinCampaignCodebaseCommit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "campaign_codebase_pins",
                columns: table => new
                {
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    RepositoryId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    TargetBranch = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    BaseCommitSha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PinnedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaign_codebase_pins", x => new { x.CampaignId, x.TargetId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_campaign_codebase_pins_TenantId_ProjectId_CampaignId",
                table: "campaign_codebase_pins",
                columns: new[] { "TenantId", "ProjectId", "CampaignId" });

            migrationBuilder.Sql("""
                ALTER TABLE public.campaign_codebase_pins OWNER TO devweave_migrator;
                REVOKE UPDATE, DELETE ON public.campaign_codebase_pins FROM devweave_app;
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    EXECUTE 'ALTER TABLE public.campaign_codebase_pins ENABLE ROW LEVEL SECURITY';
                    EXECUTE 'DROP POLICY IF EXISTS campaign_codebase_pins_tenant_isolation ON public.campaign_codebase_pins';
                    EXECUTE
                        'CREATE POLICY campaign_codebase_pins_tenant_isolation ON public.campaign_codebase_pins '
                        || 'AS PERMISSIVE FOR ALL TO devweave_app '
                        || 'USING ("TenantId" = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid) '
                        || 'WITH CHECK ("TenantId" = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid)';
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP POLICY IF EXISTS campaign_codebase_pins_tenant_isolation ON public.campaign_codebase_pins;
                """);

            migrationBuilder.DropTable(
                name: "campaign_codebase_pins");
        }
    }
}
