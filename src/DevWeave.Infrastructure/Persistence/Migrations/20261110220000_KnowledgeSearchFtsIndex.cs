using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevWeave.Infrastructure.Persistence.Migrations;

[DbContext(typeof(DevWeaveDbContext))]
[Migration("20261110220000_KnowledgeSearchFtsIndex")]
public sealed class KnowledgeSearchFtsIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE INDEX ix_knowledge_revisions_content_search
            ON knowledge_revisions
            USING GIN (to_tsvector('simple'::regconfig, "Content"::text));
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP INDEX IF EXISTS ix_knowledge_revisions_content_search;");
    }
}
