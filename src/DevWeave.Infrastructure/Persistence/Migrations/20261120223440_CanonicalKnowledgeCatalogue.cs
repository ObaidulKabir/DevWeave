using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DevWeave.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CanonicalKnowledgeCatalogue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "knowledge_type_definitions",
                columns: table => new
                {
                    TypeId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Owner = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    StorageKind = table.Column<int>(type: "integer", nullable: false),
                    LifecyclePolicy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DefaultClassification = table.Column<int>(type: "integer", nullable: false),
                    IsCanonical = table.Column<bool>(type: "boolean", nullable: false),
                    MigrationStrategy = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_type_definitions", x => x.TypeId);
                });

            migrationBuilder.InsertData(
                table: "knowledge_type_definitions",
                columns: new[] { "TypeId", "CreatedAt", "DefaultClassification", "Description", "IsCanonical", "LifecyclePolicy", "MigrationStrategy", "Owner", "StorageKind" },
                values: new object[,]
                {
                    { "architecture.component", new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "Logical or deployable component.", true, "independent-review-verification-currency", "preserve-plan-star-aliases; prefer-canonical-for-new-items", "platform", 1 },
                    { "architecture.decision", new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "Architecture decision and rationale.", true, "independent-review-verification-currency", "preserve-plan-star-aliases; prefer-canonical-for-new-items", "platform", 1 },
                    { "architecture.interface", new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "Contract between components or systems.", true, "independent-review-verification-currency", "preserve-plan-star-aliases; prefer-canonical-for-new-items", "platform", 1 },
                    { "delivery.deployment", new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "External deployment record.", true, "independent-review-verification-currency", "preserve-plan-star-aliases; prefer-canonical-for-new-items", "platform", 1 },
                    { "delivery.release", new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "Release candidate or released state.", true, "independent-review-verification-currency", "preserve-plan-star-aliases; prefer-canonical-for-new-items", "platform", 1 },
                    { "execution.activity-report", new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "Human or agent work summary.", true, "independent-review-verification-currency", "map-to-existing-aggregate; do-not-duplicate-as-knowledge", "platform", 3 },
                    { "experience.feedback", new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "User or stakeholder feedback.", true, "independent-review-verification-currency", "preserve-plan-star-aliases; prefer-canonical-for-new-items", "platform", 1 },
                    { "governance.risk", new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 3, "Uncertainty with impact.", true, "independent-review-verification-currency", "preserve-plan-star-aliases; prefer-canonical-for-new-items", "platform", 1 },
                    { "learning.lesson", new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "Educational explanation or retrospective.", true, "independent-review-verification-currency", "preserve-plan-star-aliases; prefer-canonical-for-new-items", "platform", 1 },
                    { "operation.incident", new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 3, "Operational event or incident.", true, "independent-review-verification-currency", "preserve-plan-star-aliases; prefer-canonical-for-new-items", "platform", 1 },
                    { "planning.work-item", new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "Planned unit of work.", true, "independent-review-verification-currency", "map-to-existing-aggregate; do-not-duplicate-as-knowledge", "platform", 2 },
                    { "requirements.constraint", new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "Design, legal, technical, or business constraint.", true, "independent-review-verification-currency", "preserve-plan-star-aliases; prefer-canonical-for-new-items", "platform", 1 },
                    { "requirements.functional", new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "Observable functional behavior.", true, "independent-review-verification-currency", "preserve-plan-star-aliases; prefer-canonical-for-new-items", "platform", 1 },
                    { "requirements.nonfunctional", new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "Measurable quality or operational requirement.", true, "independent-review-verification-currency", "preserve-plan-star-aliases; prefer-canonical-for-new-items", "platform", 1 },
                    { "specification.technical", new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "Detailed technical specification.", true, "independent-review-verification-currency", "preserve-plan-star-aliases; prefer-canonical-for-new-items", "platform", 1 },
                    { "strategy.assumption", new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "Assumption requiring validation.", true, "independent-review-verification-currency", "preserve-plan-star-aliases; prefer-canonical-for-new-items", "platform", 1 },
                    { "strategy.goal", new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "Business/product objective.", true, "independent-review-verification-currency", "preserve-plan-star-aliases; prefer-canonical-for-new-items", "platform", 1 },
                    { "verification.result", new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "Test or assessment result.", true, "independent-review-verification-currency", "map-to-existing-aggregate; do-not-duplicate-as-knowledge", "platform", 4 }
                });

            migrationBuilder.InsertData(
                table: "schema_definitions",
                columns: new[] { "Id", "CreatedAt", "Description", "Document", "Namespace", "Owner", "PublishedAt", "Status", "Version" },
                values: new object[,]
                {
                    { new Guid("0d2f3d9e-609d-3e4a-a818-e21f0d554063"), new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Uncertainty with impact.", "{ \"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"type\": \"object\", \"additionalProperties\": false, \"required\": [\"description\"], \"properties\": { \"description\": { \"type\": \"string\", \"minLength\": 1 }, \"likelihood\": { \"type\": \"string\" }, \"impact\": { \"type\": \"string\" }, \"mitigation\": { \"type\": \"string\" }, \"owner\": { \"type\": \"string\" }, \"status\": { \"type\": \"string\" } } }", "governance.risk", "platform", new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "1.0" },
                    { new Guid("3226a05c-3df9-704c-9f4a-267c1a08464e"), new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Educational explanation or retrospective.", "{ \"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"type\": \"object\", \"additionalProperties\": false, \"required\": [\"topic\", \"explanation\"], \"properties\": { \"topic\": { \"type\": \"string\", \"minLength\": 1 }, \"audience\": { \"type\": \"string\" }, \"explanation\": { \"type\": \"string\", \"minLength\": 1 }, \"learningObjectives\": { \"type\": \"array\", \"items\": { \"type\": \"string\" } }, \"sourceRefs\": { \"type\": \"array\", \"items\": { \"type\": \"string\" } } } }", "learning.lesson", "platform", new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "1.0" },
                    { new Guid("3e6f01ad-be3b-5b47-82ef-83555f2fd43f"), new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Contract between components or systems.", "{ \"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"type\": \"object\", \"additionalProperties\": false, \"required\": [\"protocol\"], \"properties\": { \"protocol\": { \"type\": \"string\", \"minLength\": 1 }, \"operations\": { \"type\": \"array\", \"items\": { \"type\": \"string\" } }, \"schemaReference\": { \"type\": \"string\" }, \"version\": { \"type\": \"string\" }, \"owner\": { \"type\": \"string\" } } }", "architecture.interface", "platform", new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "1.0" },
                    { new Guid("549e21fb-6a7f-a34d-9d96-9df60e8daced"), new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "External deployment record.", "{ \"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"type\": \"object\", \"additionalProperties\": false, \"required\": [\"releaseRef\", \"environment\"], \"properties\": { \"releaseRef\": { \"type\": \"string\", \"minLength\": 1 }, \"environment\": { \"type\": \"string\", \"minLength\": 1 }, \"providerRef\": { \"type\": \"string\" }, \"status\": { \"type\": \"string\" }, \"timestamp\": { \"type\": \"string\" } } }", "delivery.deployment", "platform", new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "1.0" },
                    { new Guid("5a5703bf-c2b1-bd42-95be-09f18a2cb9a7"), new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Assumption requiring validation.", "{ \"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"type\": \"object\", \"additionalProperties\": false, \"required\": [\"statement\"], \"properties\": { \"statement\": { \"type\": \"string\", \"minLength\": 1 }, \"source\": { \"type\": \"string\" }, \"confidence\": { \"type\": \"string\" }, \"validationOwner\": { \"type\": \"string\" }, \"dueDate\": { \"type\": \"string\" } } }", "strategy.assumption", "platform", new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "1.0" },
                    { new Guid("5fd7a6c7-962f-ae4e-9b9a-f4945c43fc93"), new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Logical or deployable component.", "{ \"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"type\": \"object\", \"additionalProperties\": false, \"required\": [\"name\", \"responsibility\"], \"properties\": { \"name\": { \"type\": \"string\", \"minLength\": 1 }, \"responsibility\": { \"type\": \"string\", \"minLength\": 1 }, \"interfaces\": { \"type\": \"array\", \"items\": { \"type\": \"string\" } }, \"dependencies\": { \"type\": \"array\", \"items\": { \"type\": \"string\" } }, \"technologyReference\": { \"type\": \"string\" } } }", "architecture.component", "platform", new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "1.0" },
                    { new Guid("68ed9d9b-f1c2-3e4a-aa2c-b0df5daee07b"), new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Release candidate or released state.", "{ \"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"type\": \"object\", \"additionalProperties\": false, \"required\": [\"version\"], \"properties\": { \"version\": { \"type\": \"string\", \"minLength\": 1 }, \"scope\": { \"type\": \"string\" }, \"approvalRefs\": { \"type\": \"array\", \"items\": { \"type\": \"string\" } }, \"releaseDate\": { \"type\": \"string\" }, \"notesRef\": { \"type\": \"string\" } } }", "delivery.release", "platform", new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "1.0" },
                    { new Guid("8e7584fa-888e-374d-afac-2a6044c15196"), new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Operational event or incident.", "{ \"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"type\": \"object\", \"additionalProperties\": false, \"required\": [\"severity\", \"summary\"], \"properties\": { \"severity\": { \"type\": \"string\", \"minLength\": 1 }, \"summary\": { \"type\": \"string\", \"minLength\": 1 }, \"affectedService\": { \"type\": \"string\" }, \"impact\": { \"type\": \"string\" }, \"resolutionRef\": { \"type\": \"string\" } } }", "operation.incident", "platform", new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "1.0" },
                    { new Guid("ab1e3ff8-70ad-7c4a-b22d-77cf79febb85"), new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Measurable quality or operational requirement.", "{ \"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"type\": \"object\", \"additionalProperties\": false, \"required\": [ \"statement\", \"qualityAttribute\", \"metric\", \"target\", \"condition\", \"verificationMethod\" ], \"properties\": { \"statement\": { \"type\": \"string\", \"minLength\": 1 }, \"qualityAttribute\": { \"type\": \"string\", \"minLength\": 1 }, \"metric\": { \"type\": \"object\", \"additionalProperties\": false, \"required\": [\"name\", \"aggregation\", \"unit\"], \"properties\": { \"name\": { \"type\": \"string\", \"minLength\": 1 }, \"aggregation\": { \"type\": \"string\", \"minLength\": 1 }, \"unit\": { \"type\": \"string\", \"minLength\": 1 } } }, \"target\": { \"type\": \"object\", \"additionalProperties\": false, \"required\": [\"operator\", \"value\"], \"properties\": { \"operator\": { \"type\": \"string\", \"enum\": [\"lt\", \"lte\", \"eq\", \"gte\", \"gt\", \"neq\"] }, \"value\": { \"type\": [\"number\", \"string\"] } } }, \"condition\": { \"type\": \"object\", \"additionalProperties\": false, \"required\": [\"workload\", \"environment\"], \"properties\": { \"workload\": { \"type\": \"string\", \"minLength\": 1 }, \"environment\": { \"type\": \"string\", \"minLength\": 1 } } }, \"verificationMethod\": { \"type\": \"string\", \"minLength\": 1 }, \"evidencePolicy\": { \"type\": \"string\" }, \"owner\": { \"type\": \"string\" }, \"rationale\": { \"type\": \"string\" }, \"source\": { \"type\": \"string\" } } }", "requirements.nonfunctional", "platform", new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "1.0" },
                    { new Guid("cc0c7a1e-aa31-144e-9540-b626e0c1290b"), new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Design, legal, technical, or business constraint.", "{ \"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"type\": \"object\", \"additionalProperties\": false, \"required\": [\"constraintType\", \"statement\"], \"properties\": { \"constraintType\": { \"type\": \"string\", \"minLength\": 1 }, \"statement\": { \"type\": \"string\", \"minLength\": 1 }, \"rationale\": { \"type\": \"string\" }, \"scope\": { \"type\": \"string\" } } }", "requirements.constraint", "platform", new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "1.0" },
                    { new Guid("e3fa9b57-58e0-7244-a609-452a7092c463"), new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Detailed technical specification.", "{ \"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"type\": \"object\", \"additionalProperties\": false, \"required\": [\"purpose\", \"normativeContent\"], \"properties\": { \"purpose\": { \"type\": \"string\", \"minLength\": 1 }, \"scope\": { \"type\": \"string\" }, \"normativeContent\": { \"type\": \"string\", \"minLength\": 1 }, \"references\": { \"type\": \"array\", \"items\": { \"type\": \"string\" } } } }", "specification.technical", "platform", new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "1.0" },
                    { new Guid("f100949d-bcce-9f46-a979-0b7befc1470e"), new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Business/product objective.", "{ \"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"type\": \"object\", \"additionalProperties\": false, \"required\": [\"statement\"], \"properties\": { \"statement\": { \"type\": \"string\", \"minLength\": 1 }, \"rationale\": { \"type\": \"string\" }, \"successMeasures\": { \"type\": \"array\", \"items\": { \"type\": \"string\" } }, \"owner\": { \"type\": \"string\" }, \"targetDate\": { \"type\": \"string\" } } }", "strategy.goal", "platform", new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "1.0" },
                    { new Guid("f147aea8-2878-a746-9b80-a6d749c63dc8"), new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Observable functional behavior.", "{ \"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"type\": \"object\", \"additionalProperties\": false, \"required\": [\"description\", \"acceptanceCriteria\"], \"properties\": { \"description\": { \"type\": \"string\", \"minLength\": 1 }, \"priority\": { \"type\": \"string\" }, \"rationale\": { \"type\": \"string\" }, \"acceptanceCriteria\": { \"type\": \"array\", \"minItems\": 1, \"items\": { \"type\": \"object\", \"additionalProperties\": false, \"required\": [\"id\", \"statement\"], \"properties\": { \"id\": { \"type\": \"string\", \"minLength\": 1 }, \"statement\": { \"type\": \"string\", \"minLength\": 1 } } } }, \"source\": { \"type\": \"string\" } } }", "requirements.functional", "platform", new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "1.0" },
                    { new Guid("f2676f67-c66d-d443-91e4-b8a3cc33e9f1"), new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Architecture decision and rationale.", "{ \"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"type\": \"object\", \"additionalProperties\": false, \"required\": [\"context\", \"decision\"], \"properties\": { \"context\": { \"type\": \"string\", \"minLength\": 1 }, \"decision\": { \"type\": \"string\", \"minLength\": 1 }, \"alternatives\": { \"type\": \"array\", \"items\": { \"type\": \"string\" } }, \"consequences\": { \"type\": \"array\", \"items\": { \"type\": \"string\" } }, \"status\": { \"type\": \"string\" } } }", "architecture.decision", "platform", new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "1.0" },
                    { new Guid("f5c301d4-8b09-f347-92f5-42cb528b3b3d"), new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "User or stakeholder feedback.", "{ \"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"type\": \"object\", \"additionalProperties\": false, \"required\": [\"source\", \"summary\"], \"properties\": { \"source\": { \"type\": \"string\", \"minLength\": 1 }, \"audience\": { \"type\": \"string\" }, \"category\": { \"type\": \"string\" }, \"summary\": { \"type\": \"string\", \"minLength\": 1 }, \"disposition\": { \"type\": \"string\" }, \"relatedItemRefs\": { \"type\": \"array\", \"items\": { \"type\": \"string\" } } } }", "experience.feedback", "platform", new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "1.0" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_type_definitions_IsCanonical",
                table: "knowledge_type_definitions",
                column: "IsCanonical");

            // Backfill custom/legacy schema namespaces (for example plan.*) that predate the catalogue.
            migrationBuilder.Sql(
                """
                INSERT INTO knowledge_type_definitions ("TypeId", "Owner", "Description", "StorageKind", "LifecyclePolicy", "DefaultClassification", "IsCanonical", "MigrationStrategy", "CreatedAt")
                SELECT DISTINCT s."Namespace", s."Owner", s."Description", 1, 'independent-review-verification-currency', 2, FALSE, 'custom-schema; retain-across-versions', TIMESTAMPTZ '2026-11-20 00:00:00+00'
                FROM schema_definitions AS s
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM knowledge_type_definitions AS t
                    WHERE t."TypeId" = s."Namespace");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "knowledge_type_definitions");

            migrationBuilder.DeleteData(
                table: "schema_definitions",
                keyColumn: "Id",
                keyValue: new Guid("0d2f3d9e-609d-3e4a-a818-e21f0d554063"));

            migrationBuilder.DeleteData(
                table: "schema_definitions",
                keyColumn: "Id",
                keyValue: new Guid("3226a05c-3df9-704c-9f4a-267c1a08464e"));

            migrationBuilder.DeleteData(
                table: "schema_definitions",
                keyColumn: "Id",
                keyValue: new Guid("3e6f01ad-be3b-5b47-82ef-83555f2fd43f"));

            migrationBuilder.DeleteData(
                table: "schema_definitions",
                keyColumn: "Id",
                keyValue: new Guid("549e21fb-6a7f-a34d-9d96-9df60e8daced"));

            migrationBuilder.DeleteData(
                table: "schema_definitions",
                keyColumn: "Id",
                keyValue: new Guid("5a5703bf-c2b1-bd42-95be-09f18a2cb9a7"));

            migrationBuilder.DeleteData(
                table: "schema_definitions",
                keyColumn: "Id",
                keyValue: new Guid("5fd7a6c7-962f-ae4e-9b9a-f4945c43fc93"));

            migrationBuilder.DeleteData(
                table: "schema_definitions",
                keyColumn: "Id",
                keyValue: new Guid("68ed9d9b-f1c2-3e4a-aa2c-b0df5daee07b"));

            migrationBuilder.DeleteData(
                table: "schema_definitions",
                keyColumn: "Id",
                keyValue: new Guid("8e7584fa-888e-374d-afac-2a6044c15196"));

            migrationBuilder.DeleteData(
                table: "schema_definitions",
                keyColumn: "Id",
                keyValue: new Guid("ab1e3ff8-70ad-7c4a-b22d-77cf79febb85"));

            migrationBuilder.DeleteData(
                table: "schema_definitions",
                keyColumn: "Id",
                keyValue: new Guid("cc0c7a1e-aa31-144e-9540-b626e0c1290b"));

            migrationBuilder.DeleteData(
                table: "schema_definitions",
                keyColumn: "Id",
                keyValue: new Guid("e3fa9b57-58e0-7244-a609-452a7092c463"));

            migrationBuilder.DeleteData(
                table: "schema_definitions",
                keyColumn: "Id",
                keyValue: new Guid("f100949d-bcce-9f46-a979-0b7befc1470e"));

            migrationBuilder.DeleteData(
                table: "schema_definitions",
                keyColumn: "Id",
                keyValue: new Guid("f147aea8-2878-a746-9b80-a6d749c63dc8"));

            migrationBuilder.DeleteData(
                table: "schema_definitions",
                keyColumn: "Id",
                keyValue: new Guid("f2676f67-c66d-d443-91e4-b8a3cc33e9f1"));

            migrationBuilder.DeleteData(
                table: "schema_definitions",
                keyColumn: "Id",
                keyValue: new Guid("f5c301d4-8b09-f347-92f5-42cb528b3b3d"));
        }
    }
}
