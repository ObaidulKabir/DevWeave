using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevWeave.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ArchiveJobsAndOutboxDispatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "outbox_messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AvailableAt",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.Sql("""UPDATE outbox_messages SET "AvailableAt" = "OccurredAt";""");

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "outbox_messages",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PoisonedAt",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "archive_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Checksum = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FormatVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IdentityMapJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_archive_jobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_archive_jobs_TenantId_ProjectId_Kind_IdempotencyKey",
                table: "archive_jobs",
                columns: new[] { "TenantId", "ProjectId", "Kind", "IdempotencyKey" },
                unique: true);

            migrationBuilder.Sql("""
                ALTER TABLE public.archive_jobs OWNER TO devweave_migrator;
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    EXECUTE 'ALTER TABLE public.archive_jobs ENABLE ROW LEVEL SECURITY';
                    EXECUTE 'DROP POLICY IF EXISTS archive_jobs_tenant_isolation ON public.archive_jobs';
                    EXECUTE
                        'CREATE POLICY archive_jobs_tenant_isolation ON public.archive_jobs '
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
                DROP POLICY IF EXISTS archive_jobs_tenant_isolation ON public.archive_jobs;
                """);

            migrationBuilder.DropTable(
                name: "archive_jobs");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "AvailableAt",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "PoisonedAt",
                table: "outbox_messages");
        }
    }
}
