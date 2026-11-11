using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevWeave.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCoordinationPlaneTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "change_sets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkPackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodebaseTargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderRef = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CommitSha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ChangedPathMetadata = table.Column<string[]>(type: "text[]", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_change_sets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "codebase_targets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    RepositoryId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    TargetBranch = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    BaseCommitSha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_codebase_targets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "plan_baselines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Goal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ApprovedRevisionIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    DependencyIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    AcceptanceCriteria = table.Column<string[]>(type: "text[]", nullable: false),
                    ExpectedPlanVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_baselines", x => x.Id);
                });

            // Ownership: Ensure the privileged migration role owns the new tables.
            migrationBuilder.Sql("""
                ALTER TABLE public.plan_baselines OWNER TO devweave_migrator;
                ALTER TABLE public.codebase_targets OWNER TO devweave_migrator;
                ALTER TABLE public.change_sets OWNER TO devweave_migrator;
                """);

            // Row-level security for tables with direct TenantId.
            migrationBuilder.Sql("""
                DO $$
                DECLARE target text;
                BEGIN
                    FOREACH target IN ARRAY ARRAY[
                        'plan_baselines', 'codebase_targets', 'change_sets'
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
            migrationBuilder.Sql("""
                DROP POLICY IF EXISTS plan_baselines_tenant_isolation ON public.plan_baselines;
                DROP POLICY IF EXISTS codebase_targets_tenant_isolation ON public.codebase_targets;
                DROP POLICY IF EXISTS change_sets_tenant_isolation ON public.change_sets;
                """);

            migrationBuilder.DropTable(
                name: "change_sets");

            migrationBuilder.DropTable(
                name: "codebase_targets");

            migrationBuilder.DropTable(
                name: "plan_baselines");
        }
    }
}
