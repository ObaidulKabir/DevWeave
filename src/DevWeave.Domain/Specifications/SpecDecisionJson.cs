using System.Text.Json;

namespace DevWeave.Domain.Specifications;

/// <summary>Parses decision content and exposes the JSON Schema that every decision revision must satisfy.</summary>
public static class SpecDecisionJson
{
    public const string TypeId = "spec.decision";
    public const string SchemaVersion = "1.0";

    public const string SchemaDocument = """
    {
      "$schema": "https://json-schema.org/draft/2020-12/schema",
      "type": "object",
      "required": ["topic", "scope", "decision"],
      "additionalProperties": false,
      "properties": {
        "topic": { "type": "string", "minLength": 1, "maxLength": 120 },
        "scope": {
          "type": "object",
          "required": ["level"],
          "additionalProperties": false,
          "properties": {
            "level": { "enum": ["platform", "tenant", "project"] },
            "applies": { "type": "array", "items": { "type": "string", "maxLength": 200 } }
          }
        },
        "decision": {
          "type": "object",
          "required": ["choice"],
          "additionalProperties": false,
          "properties": {
            "choice": { "type": "string", "minLength": 1, "maxLength": 120 },
            "params": {
              "type": "object",
              "additionalProperties": { "type": ["string", "integer", "boolean"] }
            }
          }
        },
        "constraints": {
          "type": "array",
          "items": {
            "type": "object",
            "required": ["rule", "value"],
            "additionalProperties": false,
            "properties": {
              "rule": { "type": "string", "minLength": 1 },
              "value": { "type": ["string", "integer", "boolean"] }
            }
          }
        },
        "locked": { "type": "boolean" },
        "rationale": { "type": "string", "maxLength": 2000 }
      }
    }
    """;

    public static string ToLevelString(SpecScopeLevel level) => level.ToString().ToLowerInvariant();

    /// <summary>Parses decision content. Returns null and a reason when the shape is not a decision.</summary>
    public static SpecDecision? TryParse(string json, out string? error)
    {
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var topic = root.GetProperty("topic").GetString();
            var levelText = root.GetProperty("scope").GetProperty("level").GetString();
            var decision = root.GetProperty("decision");
            var choice = decision.GetProperty("choice").GetString();
            if (string.IsNullOrWhiteSpace(topic) || string.IsNullOrWhiteSpace(choice) ||
                !Enum.TryParse<SpecScopeLevel>(levelText, ignoreCase: true, out var level))
            {
                error = "Decision is missing topic, scope level or choice.";
                return null;
            }

            var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
            if (decision.TryGetProperty("params", out var p) && p.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in p.EnumerateObject())
                    parameters[prop.Name] = Scalar(prop.Value);
            }

            var constraints = new List<SpecConstraint>();
            if (root.TryGetProperty("constraints", out var c) && c.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in c.EnumerateArray())
                {
                    constraints.Add(new SpecConstraint(
                        item.GetProperty("rule").GetString() ?? string.Empty,
                        Scalar(item.GetProperty("value"))));
                }
            }

            var locked = root.TryGetProperty("locked", out var l) && l.ValueKind == JsonValueKind.True;
            var rationale = root.TryGetProperty("rationale", out var r) ? r.GetString() : null;
            return new SpecDecision(topic, level, choice, parameters, constraints, locked, rationale);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            error = "Content is not a valid specification decision.";
            return null;
        }
    }

    public static string Scalar(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => value.GetRawText(),
    };
}
