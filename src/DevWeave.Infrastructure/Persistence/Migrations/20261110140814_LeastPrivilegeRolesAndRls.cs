using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevWeave.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// H2 database defense in depth (architecture 15.2/16.2; TEN-002, DATA-03, REV-001/006).
    /// Establishes a privileged migration role and a least-privilege application role, enforces
    /// tenant/project ownership tuples with composite foreign keys, applies row-level security to
    /// every tenant-scoped table, and denies UPDATE/DELETE on append-only history tables for the
    /// ordinary application role.
    /// <para>
    /// The application role (<c>devweave_app</c>) is a framework role without LOGIN: environments
    /// provision a login role and grant it membership, or connect directly as this role. Applying
    /// this migration requires a role that may CREATE ROLE and reassign table ownership (a superuser
    /// or a role with CREATEROLE plus membership in <c>devweave_migrator</c>).
    /// </para>
    /// </summary>
    public partial class LeastPrivilegeRolesAndRls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Roles: a privileged migration/owner role and a least-privilege application role.
            //    Roles are cluster-scoped, so creation is guarded to stay idempotent across the many
            //    databases one server hosts in development and CI.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'devweave_migrator') THEN
                        CREATE ROLE devweave_migrator NOLOGIN;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'devweave_app') THEN
                        CREATE ROLE devweave_app NOLOGIN;
                    END IF;
                END $$;
                """);

            // 2. The privileged migration role owns the application schema objects; the application
            //    role must never own them, otherwise it would bypass row-level security as the owner.
            migrationBuilder.Sql("""
                DO $$
                DECLARE target record;
                BEGIN
                    FOR target IN
                        SELECT tablename FROM pg_tables
                        WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory'
                    LOOP
                        EXECUTE format('ALTER TABLE public.%I OWNER TO devweave_migrator', target.tablename);
                    END LOOP;
                END $$;
                """);

            // 3. Least privilege: the application role gets data access only — no DDL, no ownership.
            //    Default privileges keep newly created tables reachable without re-running this migration.
            migrationBuilder.Sql("GRANT USAGE ON SCHEMA public TO devweave_app;");
            migrationBuilder.Sql("GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO devweave_app;");
            migrationBuilder.Sql("GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO devweave_app;");
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    EXECUTE format(
                        'ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO devweave_app',
                        current_user);
                    EXECUTE format(
                        'ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO devweave_app',
                        'devweave_migrator');
                END $$;
                """);

            // 4. Append-only history: the ordinary application role may not rewrite or erase
            //    revisions, activities or package entries (REV-001/006, PRO-003/004). Exceptional,
            //    audited correction runs against the privileged migration role, not this role.
            migrationBuilder.Sql("REVOKE UPDATE, DELETE ON public.knowledge_revisions FROM devweave_app;");
            migrationBuilder.Sql("REVOKE UPDATE, DELETE ON public.activities FROM devweave_app;");
            migrationBuilder.Sql("REVOKE UPDATE, DELETE ON public.activity_inputs FROM devweave_app;");
            migrationBuilder.Sql("REVOKE UPDATE, DELETE ON public.activity_outputs FROM devweave_app;");
            migrationBuilder.Sql("REVOKE UPDATE, DELETE ON public.context_package_entries FROM devweave_app;");

            // 5. Composite ownership: separate foreign keys to the tenant and to the parent do not,
            //    on their own, prevent a child from naming a parent owned by a different tenant.
            //    A composite (parent, tenant) key plus composite foreign keys make those mismatched
            //    tuples unrepresentable (TEN-002, DATA-03).
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX IF NOT EXISTS ux_lifecycle_profiles_id_tenant
                    ON public.lifecycle_profiles ("Id", "TenantId");
                CREATE UNIQUE INDEX IF NOT EXISTS ux_projects_id_tenant
                    ON public.projects ("Id", "TenantId");
                CREATE UNIQUE INDEX IF NOT EXISTS ux_knowledge_items_id_tenant
                    ON public.knowledge_items ("Id", "TenantId");
                """);

            migrationBuilder.Sql("""
                ALTER TABLE public.projects
                    ADD CONSTRAINT fk_projects_lifecycle_profile_tenant
                    FOREIGN KEY ("LifecycleProfileId", "TenantId")
                    REFERENCES public.lifecycle_profiles ("Id", "TenantId");
                ALTER TABLE public.project_references
                    ADD CONSTRAINT fk_project_references_project_tenant
                    FOREIGN KEY ("ProjectId", "TenantId")
                    REFERENCES public.projects ("Id", "TenantId");
                ALTER TABLE public.project_grants
                    ADD CONSTRAINT fk_project_grants_project_tenant
                    FOREIGN KEY ("ProjectId", "TenantId")
                    REFERENCES public.projects ("Id", "TenantId");
                ALTER TABLE public.project_memberships
                    ADD CONSTRAINT fk_project_memberships_project_tenant
                    FOREIGN KEY ("ProjectId", "TenantId")
                    REFERENCES public.projects ("Id", "TenantId");
                ALTER TABLE public.knowledge_items
                    ADD CONSTRAINT fk_knowledge_items_project_tenant
                    FOREIGN KEY ("ProjectId", "TenantId")
                    REFERENCES public.projects ("Id", "TenantId");
                ALTER TABLE public.knowledge_revisions
                    ADD CONSTRAINT fk_knowledge_revisions_item_tenant
                    FOREIGN KEY ("KnowledgeItemId", "TenantId")
                    REFERENCES public.knowledge_items ("Id", "TenantId");
                ALTER TABLE public.relationships
                    ADD CONSTRAINT fk_relationships_source_item_tenant
                    FOREIGN KEY ("SourceItemId", "TenantId")
                    REFERENCES public.knowledge_items ("Id", "TenantId");
                ALTER TABLE public.relationships
                    ADD CONSTRAINT fk_relationships_target_item_tenant
                    FOREIGN KEY ("TargetItemId", "TenantId")
                    REFERENCES public.knowledge_items ("Id", "TenantId");
                ALTER TABLE public.activities
                    ADD CONSTRAINT fk_activities_project_tenant
                    FOREIGN KEY ("ProjectId", "TenantId")
                    REFERENCES public.projects ("Id", "TenantId");
                ALTER TABLE public.artifacts
                    ADD CONSTRAINT fk_artifacts_project_tenant
                    FOREIGN KEY ("ProjectId", "TenantId")
                    REFERENCES public.projects ("Id", "TenantId");
                ALTER TABLE public.evidence
                    ADD CONSTRAINT fk_evidence_project_tenant
                    FOREIGN KEY ("ProjectId", "TenantId")
                    REFERENCES public.projects ("Id", "TenantId");
                ALTER TABLE public.context_packages
                    ADD CONSTRAINT fk_context_packages_project_tenant
                    FOREIGN KEY ("ProjectId", "TenantId")
                    REFERENCES public.projects ("Id", "TenantId");
                """);

            // A tenant-wide agent grant carries a null project; MATCH SIMPLE leaves that tuple
            // unchecked while still forcing any named project to belong to the same tenant (AGT-002).
            migrationBuilder.Sql("""
                ALTER TABLE public.agent_grants
                    ADD CONSTRAINT fk_agent_grants_project_tenant
                    FOREIGN KEY ("ProjectId", "TenantId")
                    REFERENCES public.projects ("Id", "TenantId");
                """);

            // 6. Row-level security. Every tenant-scoped table filters on a per-session tenant setting,
            //    so a query that forgets its tenant predicate still cannot disclose another tenant's
            //    rows. An unset or empty setting matches nothing: the policy fails closed (TEN-002).
            //    The policies bind to the application role only; the owner (migration) role is exempt.
            migrationBuilder.Sql("""
                DO $$
                DECLARE target text;
                BEGIN
                    FOREACH target IN ARRAY ARRAY[
                        'principals', 'memberships', 'lifecycle_profiles', 'projects',
                        'project_references', 'project_memberships', 'project_grants',
                        'knowledge_items', 'knowledge_revisions', 'relationships',
                        'activities', 'artifacts', 'evidence', 'agent_grants',
                        'agent_credentials', 'context_packages'
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

            // Owned child tables carry no tenant column of their own; they inherit the parent's
            // tenant through an existence check against the owning row.
            migrationBuilder.Sql("""
                DO $$
                DECLARE spec record;
                BEGIN
                    FOR spec IN
                        SELECT * FROM (VALUES
                            ('activity_inputs', 'activities', 'activity_id'),
                            ('activity_outputs', 'activities', 'activity_id'),
                            ('context_package_entries', 'context_packages', 'context_package_id')
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
            // Roles are cluster-scoped and may back other databases on the same server, so they are
            // left in place; this reverses the per-database constraints, policies and grants only.
            migrationBuilder.Sql("""
                DO $$
                DECLARE spec record;
                BEGIN
                    FOR spec IN
                        SELECT * FROM (VALUES
                            ('activity_inputs'), ('activity_outputs'), ('context_package_entries')
                        ) AS t(child)
                    LOOP
                        EXECUTE format('DROP POLICY IF EXISTS %I ON public.%I', spec.child || '_tenant_isolation', spec.child);
                        EXECUTE format('ALTER TABLE public.%I DISABLE ROW LEVEL SECURITY', spec.child);
                    END LOOP;
                END $$;
                """);

            migrationBuilder.Sql("""
                DO $$
                DECLARE target text;
                BEGIN
                    FOREACH target IN ARRAY ARRAY[
                        'principals', 'memberships', 'lifecycle_profiles', 'projects',
                        'project_references', 'project_memberships', 'project_grants',
                        'knowledge_items', 'knowledge_revisions', 'relationships',
                        'activities', 'artifacts', 'evidence', 'agent_grants',
                        'agent_credentials', 'context_packages'
                    ]
                    LOOP
                        EXECUTE format('DROP POLICY IF EXISTS %I ON public.%I', target || '_tenant_isolation', target);
                        EXECUTE format('ALTER TABLE public.%I DISABLE ROW LEVEL SECURITY', target);
                    END LOOP;
                END $$;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE public.agent_grants DROP CONSTRAINT IF EXISTS fk_agent_grants_project_tenant;
                ALTER TABLE public.context_packages DROP CONSTRAINT IF EXISTS fk_context_packages_project_tenant;
                ALTER TABLE public.evidence DROP CONSTRAINT IF EXISTS fk_evidence_project_tenant;
                ALTER TABLE public.artifacts DROP CONSTRAINT IF EXISTS fk_artifacts_project_tenant;
                ALTER TABLE public.activities DROP CONSTRAINT IF EXISTS fk_activities_project_tenant;
                ALTER TABLE public.relationships DROP CONSTRAINT IF EXISTS fk_relationships_target_item_tenant;
                ALTER TABLE public.relationships DROP CONSTRAINT IF EXISTS fk_relationships_source_item_tenant;
                ALTER TABLE public.knowledge_revisions DROP CONSTRAINT IF EXISTS fk_knowledge_revisions_item_tenant;
                ALTER TABLE public.knowledge_items DROP CONSTRAINT IF EXISTS fk_knowledge_items_project_tenant;
                ALTER TABLE public.project_memberships DROP CONSTRAINT IF EXISTS fk_project_memberships_project_tenant;
                ALTER TABLE public.project_grants DROP CONSTRAINT IF EXISTS fk_project_grants_project_tenant;
                ALTER TABLE public.project_references DROP CONSTRAINT IF EXISTS fk_project_references_project_tenant;
                ALTER TABLE public.projects DROP CONSTRAINT IF EXISTS fk_projects_lifecycle_profile_tenant;
                DROP INDEX IF EXISTS public.ux_knowledge_items_id_tenant;
                DROP INDEX IF EXISTS public.ux_projects_id_tenant;
                DROP INDEX IF EXISTS public.ux_lifecycle_profiles_id_tenant;
                """);

            migrationBuilder.Sql("REVOKE ALL ON ALL TABLES IN SCHEMA public FROM devweave_app;");
            migrationBuilder.Sql("REVOKE ALL ON ALL SEQUENCES IN SCHEMA public FROM devweave_app;");
        }
    }
}
