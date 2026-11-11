using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevWeave.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SecurityHardeningV3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Ownership: Ensure the privileged migration role owns the new tables.
            migrationBuilder.Sql("""
                ALTER TABLE public.coordination_channels OWNER TO devweave_migrator;
                ALTER TABLE public.coordination_messages OWNER TO devweave_migrator;
                ALTER TABLE public.coordination_cursors OWNER TO devweave_migrator;
                ALTER TABLE public.coordination_events OWNER TO devweave_migrator;
                ALTER TABLE public.execution_campaigns OWNER TO devweave_migrator;
                ALTER TABLE public.work_packages OWNER TO devweave_migrator;
                ALTER TABLE public.task_leases OWNER TO devweave_migrator;
                ALTER TABLE public.agent_runtime_instances OWNER TO devweave_migrator;
                """);

            // 2. Append-only history: Revoke UPDATE/DELETE on coordination messages and events for the application role.
            migrationBuilder.Sql("REVOKE UPDATE, DELETE ON public.coordination_messages FROM devweave_app;");
            migrationBuilder.Sql("REVOKE UPDATE, DELETE ON public.coordination_events FROM devweave_app;");

            // 3. Row-level security for tables with direct TenantId.
            migrationBuilder.Sql("""
                DO $$
                DECLARE target text;
                BEGIN
                    FOREACH target IN ARRAY ARRAY[
                        'coordination_channels', 'coordination_messages', 'coordination_cursors', 
                        'coordination_events', 'execution_campaigns', 'work_packages', 
                        'task_leases', 'agent_runtime_instances'
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
