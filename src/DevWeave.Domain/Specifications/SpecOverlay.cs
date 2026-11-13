using System.Text.Json;

namespace DevWeave.Domain.Specifications;

public sealed record SpecChoiceRef(string Topic, string Choice);

public sealed record SpecChoiceAddition(string Topic, SpecChoice Choice);

/// <summary>Holds a tenant or project on one version of a built-in pack until a person approves moving on.</summary>
public sealed record SpecPackPin(string Pack, string Version);

/// <summary>
/// A tenant- or project-level extension of the built-in catalog. Overlays only add or narrow; they can
/// never change a locked platform decision or replace a built-in topic.
/// </summary>
public sealed record SpecOverlay(
    SpecScopeLevel Level,
    string? Namespace,
    IReadOnlyList<string> EnablePacks,
    IReadOnlyList<SpecPackPin> PinPacks,
    IReadOnlyList<SpecTopic> AddTopics,
    IReadOnlyList<SpecChoiceAddition> AddChoices,
    IReadOnlyList<SpecChoiceRef> HideChoices,
    IReadOnlyList<SpecChoiceRef> DeprecateChoices,
    IReadOnlyList<SpecChoiceRef> SetDefaults,
    IReadOnlyList<SpecProfile> AddProfiles,
    string? Rationale);

public static class SpecOverlayJson
{
    public const string TypeId = "spec.overlay";
    public const string SchemaVersion = "1.0";

    /// <summary>Structural schema. Semantic rules (namespaces, locks, references) are enforced by the catalog builder.</summary>
    public const string SchemaDocument = """
    {
      "$schema": "https://json-schema.org/draft/2020-12/schema",
      "type": "object",
      "required": ["scope"],
      "additionalProperties": false,
      "properties": {
        "scope": {
          "type": "object", "required": ["level"], "additionalProperties": false,
          "properties": { "level": { "enum": ["tenant", "project"] } }
        },
        "namespace": { "type": "string", "pattern": "^[a-z][a-z0-9-]{1,19}$" },
        "enablePacks": { "type": "array", "items": { "type": "string" } },
        "pinPacks": { "type": "array", "items": { "type": "object", "required": ["pack", "version"] } },
        "addTopics": { "type": "array", "items": { "type": "object" } },
        "addChoices": { "type": "array", "items": { "type": "object", "required": ["topic", "choice"] } },
        "hideChoices": { "type": "array", "items": { "type": "object", "required": ["topic", "choice"] } },
        "deprecateChoices": { "type": "array", "items": { "type": "object", "required": ["topic", "choice"] } },
        "setDefaults": { "type": "array", "items": { "type": "object", "required": ["topic", "choice"] } },
        "addProfiles": { "type": "array", "items": { "type": "object" } },
        "rationale": { "type": "string", "maxLength": 2000 }
      }
    }
    """;

    public static SpecOverlay? TryParse(string json, out string? error)
    {
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!Enum.TryParse<SpecScopeLevel>(root.GetProperty("scope").GetProperty("level").GetString(), true, out var level) ||
                level == SpecScopeLevel.Platform)
            {
                error = "Overlay scope must be tenant or project.";
                return null;
            }

            var source = level.ToString().ToLowerInvariant();
            return new SpecOverlay(
                level,
                SpecCatalogJson.OptStr(root, "namespace"),
                SpecCatalogJson.Array(root, "enablePacks").Select(x => x.GetString() ?? string.Empty).ToArray(),
                SpecCatalogJson.Array(root, "pinPacks")
                    .Select(x => new SpecPackPin(SpecCatalogJson.Str(x, "pack"), SpecCatalogJson.Str(x, "version")))
                    .ToArray(),
                SpecCatalogJson.Array(root, "addTopics").Select(t => SpecCatalogJson.ReadTopic(t, source)).ToArray(),
                SpecCatalogJson.Array(root, "addChoices")
                    .Select(a => new SpecChoiceAddition(SpecCatalogJson.Str(a, "topic"), SpecCatalogJson.ReadChoice(a.GetProperty("choice"), source)))
                    .ToArray(),
                Refs(root, "hideChoices"),
                Refs(root, "deprecateChoices"),
                Refs(root, "setDefaults"),
                SpecCatalogJson.Array(root, "addProfiles").Select(p => SpecCatalogJson.ReadProfile(p, source)).ToArray(),
                SpecCatalogJson.OptStr(root, "rationale"));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            error = ex is FormatException ? ex.Message : "Content is not a valid catalog overlay.";
            return null;
        }
    }

    private static SpecChoiceRef[] Refs(JsonElement root, string name) =>
        SpecCatalogJson.Array(root, name)
            .Select(r => new SpecChoiceRef(SpecCatalogJson.Str(r, "topic"), SpecCatalogJson.Str(r, "choice")))
            .ToArray();
}
