using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevWeave.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnableRlsOnProjectGraphTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent backfill for environments that already applied ProjectGraphJournalAndBaselineV2
            // before OWNER/RLS SQL was added to that migration.
            migrationBuilder.Sql("""
                ALTER TABLE public.project_events OWNER TO devweave_migrator;
                ALTER TABLE public.plan_baseline_manifests OWNER TO devweave_migrator;
                ALTER TABLE public.project_event_inputs OWNER TO devweave_migrator;
                ALTER TABLE public.project_event_outputs OWNER TO devweave_migrator;
                ALTER TABLE public.plan_baseline_revision_refs OWNER TO devweave_migrator;
                ALTER TABLE public.plan_baseline_semantic_edges OWNER TO devweave_migrator;
                ALTER TABLE public.plan_baseline_specification_bindings OWNER TO devweave_migrator;
                ALTER TABLE public.plan_baseline_criterion_refs OWNER TO devweave_migrator;
                ALTER TABLE public.plan_baseline_dependency_edges OWNER TO devweave_migrator;
                """);

            migrationBuilder.Sql("""
                DO $$
                DECLARE target text;
                BEGIN
                    FOREACH target IN ARRAY ARRAY[
                        'project_events', 'plan_baseline_manifests'
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

            migrationBuilder.Sql("""
                DO $$
                DECLARE spec record;
                BEGIN
                    FOR spec IN
                        SELECT * FROM (VALUES
                            ('project_event_inputs', 'project_events', 'project_event_id'),
                            ('project_event_outputs', 'project_events', 'project_event_id'),
                            ('plan_baseline_revision_refs', 'plan_baseline_manifests', 'baseline_id'),
                            ('plan_baseline_semantic_edges', 'plan_baseline_manifests', 'baseline_id'),
                            ('plan_baseline_specification_bindings', 'plan_baseline_manifests', 'baseline_id'),
                            ('plan_baseline_criterion_refs', 'plan_baseline_manifests', 'baseline_id'),
                            ('plan_baseline_dependency_edges', 'plan_baseline_manifests', 'baseline_id')
                        ) AS t(child, parent, fk)
                    LOOP
                        EXECUTE format('ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY', spec.child);
                        EXECUTE format('DROP POLICY IF EXISTS %I ON public.%I', spec.child || '_tenant_isolation', spec.child);
                        EXECUTE format(
                            'CREATE POLICY %I ON public.%I AS PERMISSIVE FOR ALL TO devweave_app '
                            || 'USING (EXISTS (SELECT 1 FROM public.%I p WHERE p."Id" = %I.%I '
                            || 'AND p."TenantId" = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid)) '
                            || 'WITH CHECK (EXISTS (SELECT 1 FROM public.%I p WHERE p."Id" = %I.%I '
                            || 'AND p."TenantId" = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid))',
                            spec.child || '_tenant_isolation', spec.child,
                            spec.parent, spec.child, spec.fk,
                            spec.parent, spec.child, spec.fk);
                    END LOOP;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP POLICY IF EXISTS project_events_tenant_isolation ON public.project_events;
                DROP POLICY IF EXISTS plan_baseline_manifests_tenant_isolation ON public.plan_baseline_manifests;
                DROP POLICY IF EXISTS project_event_inputs_tenant_isolation ON public.project_event_inputs;
                DROP POLICY IF EXISTS project_event_outputs_tenant_isolation ON public.project_event_outputs;
                DROP POLICY IF EXISTS plan_baseline_revision_refs_tenant_isolation ON public.plan_baseline_revision_refs;
                DROP POLICY IF EXISTS plan_baseline_semantic_edges_tenant_isolation ON public.plan_baseline_semantic_edges;
                DROP POLICY IF EXISTS plan_baseline_specification_bindings_tenant_isolation ON public.plan_baseline_specification_bindings;
                DROP POLICY IF EXISTS plan_baseline_criterion_refs_tenant_isolation ON public.plan_baseline_criterion_refs;
                DROP POLICY IF EXISTS plan_baseline_dependency_edges_tenant_isolation ON public.plan_baseline_dependency_edges;
                """);
        }
    }
}
