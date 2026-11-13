using System.Text.Json;

namespace DevWeave.Domain.Specifications;

/// <summary>
/// Several decisions proposed and approved together, usually from a profile with edits. One approval
/// makes every decision in the bundle binding; each decision is still checked against its topic.
/// </summary>
public sealed record SpecBundle(
    string? ProfileId,
    SpecScopeLevel Level,
    IReadOnlyList<SpecProfileDecision> Decisions,
    string? Rationale);

public static class SpecBundleJson
{
    public const string TypeId = "spec.bundle";
    public const string SchemaVersion = "1.0";

    public const string SchemaDocument = """
    {
      "$schema": "https://json-schema.org/draft/2020-12/schema",
      "type": "object",
      "required": ["scope", "decisions"],
      "additionalProperties": false,
      "properties": {
        "profile": { "type": "string", "maxLength": 120 },
        "scope": {
          "type": "object", "required": ["level"], "additionalProperties": false,
          "properties": { "level": { "enum": ["tenant", "project"] } }
        },
        "decisions": {
          "type": "array", "minItems": 1, "maxItems": 100,
          "items": {
            "type": "object", "required": ["topic", "choice"], "additionalProperties": false,
            "properties": {
              "topic": { "type": "string", "minLength": 1 },
              "choice": { "type": "string", "minLength": 1 },
              "params": { "type": "object", "additionalProperties": { "type": ["string", "integer", "boolean"] } }
            }
          }
        },
        "rationale": { "type": "string", "maxLength": 2000 }
      }
    }
    """;

    public static SpecBundle? TryParse(string json, out string? error)
    {
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!Enum.TryParse<SpecScopeLevel>(root.GetProperty("scope").GetProperty("level").GetString(), true, out var level) ||
                level == SpecScopeLevel.Platform)
            {
                error = "Bundle scope must be tenant or project.";
                return null;
            }

            var decisions = SpecCatalogJson.ReadDecisions(root);
            return new SpecBundle(SpecCatalogJson.OptStr(root, "profile"), level, decisions, SpecCatalogJson.OptStr(root, "rationale"));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            error = "Content is not a valid decision bundle.";
            return null;
        }
    }

    /// <summary>Serialises a bundle to the stored JSON shape.</summary>
    public static string ToJson(SpecBundle bundle) => JsonSerializer.Serialize(new Dictionary<string, object?>
    {
        ["profile"] = bundle.ProfileId,
        ["scope"] = new { level = SpecDecisionJson.ToLevelString(bundle.Level) },
        ["decisions"] = bundle.Decisions.Select(d => new Dictionary<string, object?>
        {
            ["topic"] = d.Topic,
            ["choice"] = d.Choice,
            ["params"] = d.Params.ToDictionary(p => p.Key, p => ParamValue(p.Value)),
        }),
        ["rationale"] = bundle.Rationale,
    }.Where(kv => kv.Value is not null).ToDictionary(kv => kv.Key, kv => kv.Value));

    private static object ParamValue(string value) =>
        value is "true" or "false" ? value == "true"
        : long.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n
        : value;
}
