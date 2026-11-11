using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevWeave.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SecurityHardeningV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Ownership: Ensure the privileged migration role owns the new tables.
            migrationBuilder.Sql("""
                ALTER TABLE public.integrations OWNER TO devweave_migrator;
                ALTER TABLE public.inbound_events OWNER TO devweave_migrator;
                ALTER TABLE public.outbox_messages OWNER TO devweave_migrator;
                ALTER TABLE public.audit_logs OWNER TO devweave_migrator;
                """);

            // 2. Append-only history: Revoke UPDATE/DELETE on audit logs and outbox for the application role.
            migrationBuilder.Sql("REVOKE UPDATE, DELETE ON public.audit_logs FROM devweave_app;");
            migrationBuilder.Sql("REVOKE UPDATE, DELETE ON public.outbox_messages FROM devweave_app;");
            migrationBuilder.Sql("REVOKE UPDATE, DELETE ON public.inbound_events FROM devweave_app;");

            // 3. Row-level security for tables with direct TenantId.
            migrationBuilder.Sql("""
                DO $$
                DECLARE target text;
                BEGIN
                    FOREACH target IN ARRAY ARRAY[
                        'integrations', 'outbox_messages', 'audit_logs'
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

            // 4. Inherited RLS for inbound_events (inherits from integrations).
            migrationBuilder.Sql("""
                ALTER TABLE public.inbound_events ENABLE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS inbound_events_tenant_isolation ON public.inbound_events;
                CREATE POLICY inbound_events_tenant_isolation ON public.inbound_events AS PERMISSIVE FOR ALL TO devweave_app
                USING (EXISTS (SELECT 1 FROM public.integrations i WHERE i."Id" = public.inbound_events."IntegrationId"
                AND i."TenantId" = NULLIF(current_setting('app.tenant_id', true), '')::uuid))
                WITH CHECK (EXISTS (SELECT 1 FROM public.integrations i WHERE i."Id" = public.inbound_events."IntegrationId"
                AND i."TenantId" = NULLIF(current_setting('app.tenant_id', true), '')::uuid));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
