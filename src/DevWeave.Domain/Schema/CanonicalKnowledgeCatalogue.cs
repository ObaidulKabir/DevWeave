using System.Security.Cryptography;
using System.Text;
using DevWeave.Domain.Knowledge;

namespace DevWeave.Domain.Schema;

/// <summary>
/// Platform-owned Appendix A catalogue entries and JSON Schema documents for Knowledge-backed
/// types. Aggregate-mapped concepts are catalogue-only and must not receive schema documents here.
/// </summary>
public static class CanonicalKnowledgeCatalogue
{
    public const string SchemaVersion = "1.0";
    public const string Owner = "platform";
    public static readonly DateTimeOffset SeededAt = new(2026, 11, 20, 0, 0, 0, TimeSpan.Zero);

    public static IReadOnlyList<KnowledgeTypeDefinition> Types { get; } =
    [
        Type("strategy.goal", "Business/product objective.", KnowledgeStorageKind.KnowledgeEnvelope, Classification.Internal),
        Type("strategy.assumption", "Assumption requiring validation.", KnowledgeStorageKind.KnowledgeEnvelope, Classification.Internal),
        Type("requirements.functional", "Observable functional behavior.", KnowledgeStorageKind.KnowledgeEnvelope, Classification.Internal),
        Type("requirements.nonfunctional", "Measurable quality or operational requirement.", KnowledgeStorageKind.KnowledgeEnvelope, Classification.Internal),
        Type("requirements.constraint", "Design, legal, technical, or business constraint.", KnowledgeStorageKind.KnowledgeEnvelope, Classification.Internal),
        Type("architecture.decision", "Architecture decision and rationale.", KnowledgeStorageKind.KnowledgeEnvelope, Classification.Internal),
        Type("architecture.component", "Logical or deployable component.", KnowledgeStorageKind.KnowledgeEnvelope, Classification.Internal),
        Type("architecture.interface", "Contract between components or systems.", KnowledgeStorageKind.KnowledgeEnvelope, Classification.Internal),
        Type("specification.technical", "Detailed technical specification.", KnowledgeStorageKind.KnowledgeEnvelope, Classification.Internal),
        Type("planning.work-item", "Planned unit of work.", KnowledgeStorageKind.WorkItemAggregate, Classification.Internal),
        Type("execution.activity-report", "Human or agent work summary.", KnowledgeStorageKind.ActivityAggregate, Classification.Internal),
        Type("verification.result", "Test or assessment result.", KnowledgeStorageKind.EvidenceAggregate, Classification.Internal),
        Type("governance.risk", "Uncertainty with impact.", KnowledgeStorageKind.KnowledgeEnvelope, Classification.Confidential),
        Type("delivery.release", "Release candidate or released state.", KnowledgeStorageKind.KnowledgeEnvelope, Classification.Internal),
        Type("delivery.deployment", "External deployment record.", KnowledgeStorageKind.KnowledgeEnvelope, Classification.Internal),
        Type("operation.incident", "Operational event or incident.", KnowledgeStorageKind.KnowledgeEnvelope, Classification.Confidential),
        Type("learning.lesson", "Educational explanation or retrospective.", KnowledgeStorageKind.KnowledgeEnvelope, Classification.Internal),
        Type("experience.feedback", "User or stakeholder feedback.", KnowledgeStorageKind.KnowledgeEnvelope, Classification.Internal),
    ];

    public static IReadOnlyDictionary<string, string> SchemaDocuments { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["strategy.goal"] = ObjectSchema(
                ("statement", "string", true),
                ("rationale", "string", false),
                ("successMeasures", "array", false),
                ("owner", "string", false),
                ("targetDate", "string", false)),
            ["strategy.assumption"] = ObjectSchema(
                ("statement", "string", true),
                ("source", "string", false),
                ("confidence", "string", false),
                ("validationOwner", "string", false),
                ("dueDate", "string", false)),
            ["requirements.functional"] = """
                {
                  "$schema": "https://json-schema.org/draft/2020-12/schema",
                  "type": "object",
                  "additionalProperties": false,
                  "required": ["description", "acceptanceCriteria"],
                  "properties": {
                    "description": { "type": "string", "minLength": 1 },
                    "priority": { "type": "string" },
                    "rationale": { "type": "string" },
                    "acceptanceCriteria": {
                      "type": "array",
                      "minItems": 1,
                      "items": {
                        "type": "object",
                        "additionalProperties": false,
                        "required": ["id", "statement"],
                        "properties": {
                          "id": { "type": "string", "minLength": 1 },
                          "statement": { "type": "string", "minLength": 1 }
                        }
                      }
                    },
                    "source": { "type": "string" }
                  }
                }
                """,
            ["requirements.nonfunctional"] = """
                {
                  "$schema": "https://json-schema.org/draft/2020-12/schema",
                  "type": "object",
                  "additionalProperties": false,
                  "required": [
                    "statement",
                    "qualityAttribute",
                    "metric",
                    "target",
                    "condition",
                    "verificationMethod"
                  ],
                  "properties": {
                    "statement": { "type": "string", "minLength": 1 },
                    "qualityAttribute": { "type": "string", "minLength": 1 },
                    "metric": {
                      "type": "object",
                      "additionalProperties": false,
                      "required": ["name", "aggregation", "unit"],
                      "properties": {
                        "name": { "type": "string", "minLength": 1 },
                        "aggregation": { "type": "string", "minLength": 1 },
                        "unit": { "type": "string", "minLength": 1 }
                      }
                    },
                    "target": {
                      "type": "object",
                      "additionalProperties": false,
                      "required": ["operator", "value"],
                      "properties": {
                        "operator": {
                          "type": "string",
                          "enum": ["lt", "lte", "eq", "gte", "gt", "neq"]
                        },
                        "value": { "type": ["number", "string"] }
                      }
                    },
                    "condition": {
                      "type": "object",
                      "additionalProperties": false,
                      "required": ["workload", "environment"],
                      "properties": {
                        "workload": { "type": "string", "minLength": 1 },
                        "environment": { "type": "string", "minLength": 1 }
                      }
                    },
                    "verificationMethod": { "type": "string", "minLength": 1 },
                    "evidencePolicy": { "type": "string" },
                    "owner": { "type": "string" },
                    "rationale": { "type": "string" },
                    "source": { "type": "string" }
                  }
                }
                """,
            ["requirements.constraint"] = ObjectSchema(
                ("constraintType", "string", true),
                ("statement", "string", true),
                ("rationale", "string", false),
                ("scope", "string", false)),
            ["architecture.decision"] = ObjectSchema(
                ("context", "string", true),
                ("decision", "string", true),
                ("alternatives", "array", false),
                ("consequences", "array", false),
                ("status", "string", false)),
            ["architecture.component"] = ObjectSchema(
                ("name", "string", true),
                ("responsibility", "string", true),
                ("interfaces", "array", false),
                ("dependencies", "array", false),
                ("technologyReference", "string", false)),
            ["architecture.interface"] = ObjectSchema(
                ("protocol", "string", true),
                ("operations", "array", false),
                ("schemaReference", "string", false),
                ("version", "string", false),
                ("owner", "string", false)),
            ["specification.technical"] = ObjectSchema(
                ("purpose", "string", true),
                ("scope", "string", false),
                ("normativeContent", "string", true),
                ("references", "array", false)),
            ["governance.risk"] = ObjectSchema(
                ("description", "string", true),
                ("likelihood", "string", false),
                ("impact", "string", false),
                ("mitigation", "string", false),
                ("owner", "string", false),
                ("status", "string", false)),
            ["delivery.release"] = ObjectSchema(
                ("version", "string", true),
                ("scope", "string", false),
                ("approvalRefs", "array", false),
                ("releaseDate", "string", false),
                ("notesRef", "string", false)),
            ["delivery.deployment"] = ObjectSchema(
                ("releaseRef", "string", true),
                ("environment", "string", true),
                ("providerRef", "string", false),
                ("status", "string", false),
                ("timestamp", "string", false)),
            ["operation.incident"] = ObjectSchema(
                ("severity", "string", true),
                ("summary", "string", true),
                ("affectedService", "string", false),
                ("impact", "string", false),
                ("resolutionRef", "string", false)),
            ["learning.lesson"] = ObjectSchema(
                ("topic", "string", true),
                ("audience", "string", false),
                ("explanation", "string", true),
                ("learningObjectives", "array", false),
                ("sourceRefs", "array", false)),
            ["experience.feedback"] = ObjectSchema(
                ("source", "string", true),
                ("audience", "string", false),
                ("category", "string", false),
                ("summary", "string", true),
                ("disposition", "string", false),
                ("relatedItemRefs", "array", false)),
        };

    public static KnowledgeTypeDefinition? Find(string typeId) =>
        Types.FirstOrDefault(type => string.Equals(type.TypeId, typeId, StringComparison.Ordinal));

    public static SchemaDefinitionId StableSchemaId(string typeId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"devweave:canonical-schema:{typeId}:{SchemaVersion}"));
        Span<byte> guid = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(guid);
        guid[6] = (byte)((guid[6] & 0x0F) | 0x40);
        guid[8] = (byte)((guid[8] & 0x3F) | 0x80);
        return new SchemaDefinitionId(new Guid(guid));
    }

    public static IEnumerable<SchemaDefinition> PublishedSchemas()
    {
        foreach (var type in Types.Where(candidate => candidate.AllowsKnowledgeEnvelope))
        {
            if (!SchemaDocuments.TryGetValue(type.TypeId, out var document))
                continue;

            yield return SchemaDefinition.CreatePublishedSeed(
                StableSchemaId(type.TypeId),
                type.TypeId,
                SchemaVersion,
                Owner,
                type.Description,
                MinifyJson(document),
                SeededAt);
        }
    }

    private static string MinifyJson(string document) =>
        string.Join(' ', document.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static KnowledgeTypeDefinition Type(
        string typeId,
        string description,
        KnowledgeStorageKind storageKind,
        Classification defaultClassification) =>
        KnowledgeTypeDefinition.Create(
            typeId,
            Owner,
            description,
            storageKind,
            "independent-review-verification-currency",
            defaultClassification,
            isCanonical: true,
            migrationStrategy: storageKind == KnowledgeStorageKind.KnowledgeEnvelope
                ? "preserve-plan-star-aliases; prefer-canonical-for-new-items"
                : "map-to-existing-aggregate; do-not-duplicate-as-knowledge",
            SeededAt);

    private static string ObjectSchema(params (string Name, string Type, bool Required)[] properties)
    {
        var required = properties.Where(p => p.Required).Select(p => $"\"{p.Name}\"").ToArray();
        var props = properties.Select(p =>
        {
            var typeJson = p.Type switch
            {
                "array" => """{ "type": "array", "items": { "type": "string" } }""",
                _ => $$"""{ "type": "{{p.Type}}"{{(p.Required && p.Type == "string" ? """, "minLength": 1""" : string.Empty)}} }""",
            };
            return $"\"{p.Name}\": {typeJson}";
        });

        return $$"""
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "object",
              "additionalProperties": false,
              "required": [{{string.Join(", ", required)}}],
              "properties": {
                {{string.Join(",\n    ", props)}}
              }
            }
            """;
    }
}
