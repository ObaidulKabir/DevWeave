using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevWeave.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssignWorkPackagesOnOneBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "work_package_assignments",
                columns: table => new
                {
                    PackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuntimeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    BaselineId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContextSnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotRevisionIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_package_assignments", x => x.PackageId);
                });

            migrationBuilder.CreateTable(
                name: "work_package_dependencies",
                columns: table => new
                {
                    PackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    DependsOnPackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    BaselineId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_package_dependencies", x => new { x.PackageId, x.DependsOnPackageId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_work_package_assignments_TenantId_CampaignId_RuntimeId",
                table: "work_package_assignments",
                columns: new[] { "TenantId", "CampaignId", "RuntimeId" });

            migrationBuilder.CreateIndex(
                name: "IX_work_package_dependencies_TenantId_CampaignId_BaselineId",
                table: "work_package_dependencies",
                columns: new[] { "TenantId", "CampaignId", "BaselineId" });

            migrationBuilder.Sql("""
                ALTER TABLE public.work_package_assignments OWNER TO devweave_migrator;
                REVOKE UPDATE, DELETE ON public.work_package_assignments FROM devweave_app;
                ALTER TABLE public.work_package_dependencies OWNER TO devweave_migrator;
                REVOKE UPDATE, DELETE ON public.work_package_dependencies FROM devweave_app;
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    EXECUTE 'ALTER TABLE public.work_package_assignments ENABLE ROW LEVEL SECURITY';
                    EXECUTE 'DROP POLICY IF EXISTS work_package_assignments_tenant_isolation ON public.work_package_assignments';
                    EXECUTE
                        'CREATE POLICY work_package_assignments_tenant_isolation ON public.work_package_assignments '
                        || 'AS PERMISSIVE FOR ALL TO devweave_app '
                        || 'USING ("TenantId" = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid) '
                        || 'WITH CHECK ("TenantId" = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid)';
                    EXECUTE 'ALTER TABLE public.work_package_dependencies ENABLE ROW LEVEL SECURITY';
                    EXECUTE 'DROP POLICY IF EXISTS work_package_dependencies_tenant_isolation ON public.work_package_dependencies';
                    EXECUTE
                        'CREATE POLICY work_package_dependencies_tenant_isolation ON public.work_package_dependencies '
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
                DROP POLICY IF EXISTS work_package_assignments_tenant_isolation ON public.work_package_assignments;
                DROP POLICY IF EXISTS work_package_dependencies_tenant_isolation ON public.work_package_dependencies;
                """);

            migrationBuilder.DropTable(
                name: "work_package_assignments");

            migrationBuilder.DropTable(
                name: "work_package_dependencies");
        }
    }
}
