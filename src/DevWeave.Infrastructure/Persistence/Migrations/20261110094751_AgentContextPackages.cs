using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DevWeave.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgentContextPackages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_credentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SecretHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_credentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agent_credentials_principals_AgentId",
                        column: x => x.AgentId,
                        principalTable: "principals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_agent_credentials_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "agent_grants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Capability = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_grants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agent_grants_principals_AgentId",
                        column: x => x.AgentId,
                        principalTable: "principals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_agent_grants_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_agent_grants_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "context_packages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_context_packages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_context_packages_principals_AgentId",
                        column: x => x.AgentId,
                        principalTable: "principals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_context_packages_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_context_packages_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "context_package_entries",
                columns: table => new
                {
                    context_package_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    knowledge_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    knowledge_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    representation_kind = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_context_package_entries", x => new { x.context_package_id, x.Id });
                    table.ForeignKey(
                        name: "FK_context_package_entries_context_packages_context_package_id",
                        column: x => x.context_package_id,
                        principalTable: "context_packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_credentials_AgentId",
                table: "agent_credentials",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_agent_credentials_TenantId_AgentId",
                table: "agent_credentials",
                columns: new[] { "TenantId", "AgentId" });

            migrationBuilder.CreateIndex(
                name: "IX_agent_grants_AgentId",
                table: "agent_grants",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_agent_grants_ProjectId",
                table: "agent_grants",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_agent_grants_TenantId_AgentId",
                table: "agent_grants",
                columns: new[] { "TenantId", "AgentId" });

            migrationBuilder.CreateIndex(
                name: "IX_context_packages_AgentId",
                table: "context_packages",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_context_packages_ProjectId",
                table: "context_packages",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_context_packages_TenantId_ProjectId_IdempotencyKey",
                table: "context_packages",
                columns: new[] { "TenantId", "ProjectId", "IdempotencyKey" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_credentials");

            migrationBuilder.DropTable(
                name: "agent_grants");

            migrationBuilder.DropTable(
                name: "context_package_entries");

            migrationBuilder.DropTable(
                name: "context_packages");
        }
    }
}
