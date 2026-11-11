using System;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevWeave.Infrastructure.Persistence.Migrations;

[DbContext(typeof(DevWeaveDbContext))]
[Migration("20261110221000_AddTenantPlanAssignments")]
public sealed class AddTenantPlanAssignments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tenant_plan_assignments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                EffectiveTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tenant_plan_assignments", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_tenant_plan_assignments_TenantId_IsActive",
            table: "tenant_plan_assignments",
            columns: new[] { "TenantId", "IsActive" });

        // Ownership: the privileged migration role owns the new table so the app role cannot bypass RLS.
        migrationBuilder.Sql("""
            ALTER TABLE public.tenant_plan_assignments OWNER TO devweave_migrator;
            """);

        // Row-level security for tables with a direct TenantId.
        migrationBuilder.Sql("""
            DO $$
            DECLARE target text;
            BEGIN
                FOREACH target IN ARRAY ARRAY[
                    'tenant_plan_assignments'
                ]
                LOOP
                    EXECUTE format('ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY', target);
                    EXECUTE format('DROP POLICY IF EXISTS %I ON public.%I', target || '_tenant_isolation', target);
                    EXECUTE format(
                        'CREATE POLICY %I ON public.%I AS PERMISSIVE FOR ALL TO devweave_app '
                        || 'USING ("TenantId" = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid) '
                        || 'WITH CHECK ("TenantId" = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid)',
                        target || '_tenant_isolation', target);
                END LOOP;
            END $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP POLICY IF EXISTS tenant_plan_assignments_tenant_isolation ON public.tenant_plan_assignments;
            """);

        migrationBuilder.DropTable(
            name: "tenant_plan_assignments");
    }
}
