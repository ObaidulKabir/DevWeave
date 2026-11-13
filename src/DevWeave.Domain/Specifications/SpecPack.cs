using System.Reflection;
using System.Text.Json;

namespace DevWeave.Domain.Specifications;

/// <summary>A built-in, versioned, immutable package of topics, profiles and predefined platform decisions.</summary>
public sealed record SpecPack(
    string Id,
    string Version,
    string Description,
    bool EnabledByDefault,
    IReadOnlyList<SpecTopic> Topics,
    IReadOnlyList<SpecProfile> Profiles,
    IReadOnlyList<SpecCandidate> Decisions)
{
    public string Source => $"{Id}@{Version}";
}

public static class SpecPackLoader
{
    private static readonly DateTimeOffset DesignTime = new(2026, 11, 9, 0, 0, 0, TimeSpan.Zero);

    public static SpecPack Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var id = SpecCatalogJson.Str(root, "id");
        var version = SpecCatalogJson.Str(root, "version");
        var source = $"{id}@{version}";

        var topics = SpecCatalogJson.Array(root, "topics").Select(t => SpecCatalogJson.ReadTopic(t, source)).ToList();
        var profiles = SpecCatalogJson.Array(root, "profiles").Select(p => SpecCatalogJson.ReadProfile(p, source)).ToList();

        var decisions = new List<SpecCandidate>();
        foreach (var d in SpecCatalogJson.Array(root, "decisions"))
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
            if (d.TryGetProperty("params", out var p) && p.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in p.EnumerateObject())
                    parameters[prop.Name] = SpecDecisionJson.Scalar(prop.Value);
            }

            var locked = d.TryGetProperty("locked", out var l) && l.ValueKind == JsonValueKind.True;
            decisions.Add(new SpecCandidate(
                new SpecDecision(
                    SpecCatalogJson.Str(d, "topic"),
                    SpecScopeLevel.Platform,
                    SpecCatalogJson.Str(d, "choice"),
                    parameters,
                    Array.Empty<SpecConstraint>(),
                    locked,
                    SpecCatalogJson.OptStr(d, "rationale") ?? $"Predefined by {source}."),
                ItemId: null, RevisionId: null, RevisionNumber: null, ProjectId: null, DefinedAt: DesignTime));
        }

        var pack = new SpecPack(
            id, version, SpecCatalogJson.OptStr(root, "description") ?? string.Empty,
            !root.TryGetProperty("enabledByDefault", out var e) || e.ValueKind != JsonValueKind.False,
            topics, profiles, decisions);
        Verify(pack);
        return pack;
    }

    /// <summary>Loads every pack embedded in the domain assembly.</summary>
    public static IReadOnlyList<SpecPack> LoadBuiltIn()
    {
        var assembly = typeof(SpecPackLoader).Assembly;
        var packs = new List<SpecPack>();
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.EndsWith(".json", StringComparison.Ordinal)).Order())
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            packs.Add(Parse(reader.ReadToEnd()));
        }

        return packs;
    }

    /// <summary>A pack must be internally consistent, so a broken data file fails fast at start-up and in tests.</summary>
    private static void Verify(SpecPack pack)
    {
        var topicIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var topic in pack.Topics)
        {
            if (!topicIds.Add(topic.Id))
                throw new FormatException($"Pack '{pack.Id}' defines topic '{topic.Id}' twice.");
            if (topic.Choices.Select(c => c.Id).Distinct().Count() != topic.Choices.Count)
                throw new FormatException($"Topic '{topic.Id}' repeats a choice id.");
            if (topic.Default is not null && topic.Choices.All(c => c.Id != topic.Default))
                throw new FormatException($"Topic '{topic.Id}' default '{topic.Default}' is not one of its choices.");
        }

        foreach (var candidate in pack.Decisions)
        {
            var topic = pack.Topics.FirstOrDefault(t => t.Id == candidate.Decision.Topic);
            if (topic is null)
                continue; // may target a topic of another pack; the catalog builder re-checks.
            var issues = SpecDecisionValidator.Validate(topic, candidate.Decision);
            if (issues.Count > 0)
                throw new FormatException($"Pack '{pack.Id}' decision for '{topic.Id}' is invalid: {issues[0].Message}");
        }
    }
}
