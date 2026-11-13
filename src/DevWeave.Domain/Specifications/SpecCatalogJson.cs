using System.Text.Json;

namespace DevWeave.Domain.Specifications;

/// <summary>Shared JSON readers for topic, choice and profile definitions used by packs and overlays.</summary>
public static class SpecCatalogJson
{
    public static SpecTopic ReadTopic(JsonElement e, string source)
    {
        var choices = new List<SpecChoice>();
        foreach (var c in Array(e, "choices"))
            choices.Add(ReadChoice(c, source));

        var rules = new List<SpecTopicRule>();
        foreach (var r in Array(e, "rules"))
        {
            rules.Add(new SpecTopicRule(
                Str(r, "whenChoice"),
                Str(r, "otherTopic"),
                Array(r, "allowedOtherChoices").Select(x => x.GetString() ?? string.Empty).ToArray()));
        }

        return new SpecTopic(
            Str(e, "id"),
            Str(e, "title"),
            OptStr(e, "description") ?? string.Empty,
            choices,
            Array(e, "constraintRules").Select(x => x.GetString() ?? string.Empty).ToArray(),
            rules,
            OptStr(e, "domain") ?? "general",
            e.TryGetProperty("allowCustom", out var ac) && ac.ValueKind == JsonValueKind.True,
            OptStr(e, "default"),
            source);
    }

    public static SpecChoice ReadChoice(JsonElement e, string source)
    {
        var parameters = new List<SpecParameter>();
        foreach (var p in Array(e, "parameters"))
        {
            var kind = Enum.TryParse<SpecValueKind>(Str(p, "kind"), ignoreCase: true, out var k)
                ? k
                : throw new FormatException($"Unknown parameter kind '{OptStr(p, "kind")}'.");
            parameters.Add(new SpecParameter(
                Str(p, "name"),
                kind,
                !p.TryGetProperty("required", out var req) || req.ValueKind != JsonValueKind.False,
                Array(p, "allowedValues").Select(x => x.GetString() ?? string.Empty).ToArray()));
        }

        var support = SpecSupport.Supported;
        if (OptStr(e, "support") is { } text && !Enum.TryParse(text, ignoreCase: true, out support))
            throw new FormatException($"Unknown support level '{text}'.");

        return new SpecChoice(Str(e, "id"), parameters, OptStr(e, "label"), support, source);
    }

    public static SpecProfile ReadProfile(JsonElement e, string source)
    {
        var decisions = new List<SpecProfileDecision>();
        foreach (var d in Array(e, "decisions"))
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
            if (d.TryGetProperty("params", out var p) && p.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in p.EnumerateObject())
                    parameters[prop.Name] = SpecDecisionJson.Scalar(prop.Value);
            }

            decisions.Add(new SpecProfileDecision(Str(d, "topic"), Str(d, "choice"), parameters));
        }

        return new SpecProfile(
            Str(e, "id"), OptStr(e, "label") ?? Str(e, "id"), OptStr(e, "description") ?? string.Empty,
            OptStr(e, "projectType") ?? "general", decisions, source);
    }

    public static IEnumerable<JsonElement> Array(JsonElement e, string name) =>
        e.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
            : Enumerable.Empty<JsonElement>();

    public static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()!
            : throw new FormatException($"'{name}' is required.");

    public static string? OptStr(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
