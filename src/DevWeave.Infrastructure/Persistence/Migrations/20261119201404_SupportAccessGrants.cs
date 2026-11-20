using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevWeave.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SupportAccessGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "support_access_grants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApproverId = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_support_access_grants", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_support_access_grants_TenantId_OperatorId",
                table: "support_access_grants",
                columns: new[] { "TenantId", "OperatorId" });

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'devweave_migrator') THEN
                        ALTER TABLE public.support_access_grants OWNER TO devweave_migrator;
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE, DELETE ON public.support_access_grants TO devweave_app;
                ALTER TABLE public.support_access_grants ENABLE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS support_access_grants_tenant_isolation ON public.support_access_grants;
                CREATE POLICY support_access_grants_tenant_isolation
                    ON public.support_access_grants AS PERMISSIVE FOR ALL TO devweave_app
                    USING ("TenantId" = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                    WITH CHECK ("TenantId" = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "support_access_grants");
        }
    }
}
