using DevWeave.Application.Abstractions;
using DevWeave.Domain.Schema;
using Json.Schema;
using System.Text.Json;

using JsonPointer = Json.Pointer.JsonPointer;

namespace DevWeave.Infrastructure.Schema;

/// <summary>
/// Adapter that validates a content JSON string against a JSON Schema Draft 2020-12 string using
/// json-everything's <c>JsonSchema.Net</c>. Evaluation results are flattened into ordered,
/// path-specific <see cref="SchemaValidationIssue"/> entries (SRS SCH-002, KNW-004).
/// </summary>
internal sealed class JsonSchemaPayloadValidator : IPayloadValidator
{
    public IReadOnlyList<SchemaValidationIssue> Validate(string contentJson, string schemaJson)
    {
        JsonSchema schema;
        try
        {
            schema = JsonSchema.FromText(schemaJson);
        }
        catch (Exception ex)
        {
            return [new SchemaValidationIssue("$", $"Invalid JSON Schema: {ex.Message}")];
        }

        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(contentJson);
            root = document.RootElement.Clone();
        }
        catch (Exception ex)
        {
            return [new SchemaValidationIssue("$", $"Invalid JSON payload: {ex.Message}")];
        }

        var result = schema.Evaluate(root, new EvaluationOptions { OutputFormat = OutputFormat.List });

        if (result.IsValid)
        {
            return Array.Empty<SchemaValidationIssue>();
        }

        var issues = new List<SchemaValidationIssue>();
        CollectIssues(result, issues);

        return issues
            .OrderBy(issue => issue.Path, StringComparer.Ordinal)
            .ThenBy(issue => issue.Message, StringComparer.Ordinal)
            .ToList();
    }

    private static void CollectIssues(EvaluationResults node, List<SchemaValidationIssue> issues)
    {
        if (node.Errors is not null)
        {
            foreach (var error in node.Errors)
            {
                issues.Add(new SchemaValidationIssue(FormatPath(node.InstanceLocation), error.Value));
            }
        }

        if (node.Details is null)
        {
            return;
        }

        foreach (var detail in node.Details)
        {
            CollectIssues(detail, issues);
        }
    }

    private static string FormatPath(JsonPointer location)
    {
        var path = location.ToString();
        return string.IsNullOrEmpty(path) ? "$" : path;
    }
}