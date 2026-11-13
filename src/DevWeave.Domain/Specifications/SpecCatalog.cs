using System.Text.RegularExpressions;

namespace DevWeave.Domain.Specifications;

/// <summary>A package and whether it is part of the effective catalog.</summary>
public sealed record SpecPackStatus(
    string Id, string Version, string Description, bool Enabled, bool Required, string LatestVersion, bool Pinned)
{
    public bool UpdateAvailable => !string.Equals(Version, LatestVersion, StringComparison.Ordinal);
}

/// <summary>An overlay that could not be applied, with the reasons.</summary>
public sealed record SpecOverlayProblem(string Source, IReadOnlyList<SpecIssue> Issues);

/// <summary>The effective catalog for one project: built-in packs merged with tenant and project overlays.</summary>
public sealed record SpecCatalog(
    IReadOnlyList<SpecPackStatus> Packs,
    IReadOnlyList<SpecTopic> Topics,
    IReadOnlyList<SpecProfile> Profiles,
    IReadOnlyList<SpecCandidate> PredefinedDecisions,
    IReadOnlyList<SpecOverlayProblem> Problems)
{
    public SpecTopic? Find(string topicId) => Topics.FirstOrDefault(t => t.Id == topicId);

    public bool IsLocked(string topicId) =>
        PredefinedDecisions.Any(c => c.Decision.Topic == topicId && c.Decision.Locked);

    /// <summary>The catalog made of the default-enabled built-in packs only.</summary>
    public static SpecCatalog Default { get; } = SpecCatalogBuilder.Build(SpecPackLoader.LoadBuiltIn(), []);
}

public static class SpecCatalogBuilder
{
    private static readonly Regex NamespacePattern = new("^[a-z][a-z0-9-]{1,19}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Merges packs with overlays (given widest first). An invalid overlay is skipped whole and reported.</summary>
    public static SpecCatalog Build(IReadOnlyList<SpecPack> allVersions, IReadOnlyList<(SpecOverlay Overlay, string Source)> overlays)
    {
        var problems = new List<SpecOverlayProblem>();

        // Several versions of a pack may ship. The newest is the default; an approved pin holds a tenant or project back.
        var latest = allVersions.GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.OrderByDescending(p => Parse(p.Version)).First());
        var chosen = new Dictionary<string, SpecPack>(latest, StringComparer.Ordinal);
        var pinned = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (overlay, source) in overlays)
        {
            foreach (var pin in overlay.PinPacks)
            {
                var match = allVersions.FirstOrDefault(p => p.Id == pin.Pack && p.Version == pin.Version);
                if (match is null)
                    problems.Add(new SpecOverlayProblem(source, [new SpecIssue("/pinPacks", $"Package '{pin.Pack}' has no version '{pin.Version}'.")]));
                else
                {
                    chosen[pin.Pack] = match;
                    pinned.Add(pin.Pack);
                }
            }
        }

        var allPacks = chosen.Values.OrderBy(p => p.Id, StringComparer.Ordinal).ToList();
        var enabled = allPacks.Where(p => p.EnabledByDefault).Select(p => p.Id).ToHashSet(StringComparer.Ordinal);

        // Pack enablement is resolved first, so a topic added later by an overlay can build on an enabled pack.
        foreach (var (overlay, source) in overlays)
        {
            var unknown = overlay.EnablePacks.Where(id => allPacks.All(p => p.Id != id)).ToArray();
            if (unknown.Length > 0)
                problems.Add(new SpecOverlayProblem(source, unknown.Select(id => new SpecIssue("/enablePacks", $"Unknown package '{id}'.")).ToArray()));
            else
                enabled.UnionWith(overlay.EnablePacks);
        }

        var topics = new List<SpecTopic>();
        var decisions = new List<SpecCandidate>();
        var profiles = new List<SpecProfile>();
        foreach (var pack in allPacks.Where(p => enabled.Contains(p.Id)))
        {
            foreach (var topic in pack.Topics)
            {
                if (topics.Any(t => t.Id == topic.Id))
                {
                    problems.Add(new SpecOverlayProblem(pack.Source, [new SpecIssue(topic.Id, "Topic is already defined by another package.")]));
                    continue;
                }

                topics.Add(topic);
            }

            decisions.AddRange(pack.Decisions.Where(d => pack.Topics.Any(t => t.Id == d.Decision.Topic) || topics.Any(t => t.Id == d.Decision.Topic)));
            profiles.AddRange(pack.Profiles);
        }

        var catalog = new SpecCatalog(
            allPacks.Select(p => new SpecPackStatus(
                p.Id, p.Version, p.Description, enabled.Contains(p.Id), p.EnabledByDefault, latest[p.Id].Version, pinned.Contains(p.Id))).ToArray(),
            topics, profiles, decisions, problems);

        var builtInRoots = allVersions.SelectMany(p => p.Topics).Select(t => Root(t.Id)).ToHashSet(StringComparer.Ordinal);
        foreach (var (overlay, source) in overlays)
        {
            var issues = Validate(catalog, overlay, builtInRoots, out var next);
            if (issues.Count == 0 && next is not null)
                catalog = next;
            else if (issues.Count > 0)
                catalog = catalog with { Problems = [.. catalog.Problems, new SpecOverlayProblem(source, issues)] };
        }

        return catalog;
    }

    /// <summary>Checks one overlay against a catalog. Returns the issues; on success also returns the merged catalog.</summary>
    public static IReadOnlyList<SpecIssue> Validate(
        SpecCatalog catalog, SpecOverlay overlay, IReadOnlySet<string> builtInRoots, out SpecCatalog? merged)
    {
        merged = null;
        var issues = new List<SpecIssue>();
        var source = overlay.Level.ToString().ToLowerInvariant();
        var topics = catalog.Topics.ToList();
        var profiles = catalog.Profiles.ToList();

        if (overlay.AddTopics.Count > 0 || overlay.AddProfiles.Count > 0)
        {
            if (overlay.Namespace is null || !NamespacePattern.IsMatch(overlay.Namespace))
                issues.Add(new SpecIssue("/namespace", "A namespace of 2-20 lowercase letters, digits or dashes is required to add topics or profiles."));
            else if (builtInRoots.Contains(overlay.Namespace))
                issues.Add(new SpecIssue("/namespace", $"'{overlay.Namespace}' is reserved by the built-in catalog."));
        }

        foreach (var topic in overlay.AddTopics)
        {
            if (overlay.Namespace is not null && !topic.Id.StartsWith(overlay.Namespace + ".", StringComparison.Ordinal))
                issues.Add(new SpecIssue("/addTopics", $"Topic '{topic.Id}' must start with '{overlay.Namespace}.'."));
            else if (topics.Any(t => t.Id == topic.Id))
                issues.Add(new SpecIssue("/addTopics", $"Topic '{topic.Id}' already exists."));
            else if (topic.Choices.Count == 0)
                issues.Add(new SpecIssue("/addTopics", $"Topic '{topic.Id}' needs at least one choice."));
            else if (topic.Choices.Select(c => c.Id).Distinct().Count() != topic.Choices.Count)
                issues.Add(new SpecIssue("/addTopics", $"Topic '{topic.Id}' repeats a choice id."));
            else
                topics.Add(topic with { Source = source });
        }

        foreach (var add in overlay.AddChoices)
        {
            var topic = Editable(topics, catalog, add.Topic, "/addChoices", issues);
            if (topic is null)
                continue;
            if (topic.Choices.Any(c => c.Id == add.Choice.Id))
                issues.Add(new SpecIssue("/addChoices", $"Choice '{add.Choice.Id}' already exists for '{topic.Id}'."));
            else
                Replace(topics, topic with { Choices = [.. topic.Choices, add.Choice with { Source = source }] });
        }

        foreach (var hide in overlay.HideChoices)
        {
            var topic = Editable(topics, catalog, hide.Topic, "/hideChoices", issues);
            if (topic is null)
                continue;
            if (topic.Choices.All(c => c.Id != hide.Choice))
                issues.Add(new SpecIssue("/hideChoices", $"Choice '{hide.Choice}' does not exist for '{topic.Id}'."));
            else if (catalog.PredefinedDecisions.Any(d => d.Decision.Topic == topic.Id && d.Decision.Choice == hide.Choice))
                issues.Add(new SpecIssue("/hideChoices", $"'{hide.Choice}' is the platform's current choice for '{topic.Id}' and cannot be hidden."));
            else if (topic.Choices.Count == 1)
                issues.Add(new SpecIssue("/hideChoices", $"'{topic.Id}' must keep at least one choice."));
            else
                Replace(topics, topic with { Choices = topic.Choices.Where(c => c.Id != hide.Choice).ToArray(), Default = topic.Default == hide.Choice ? null : topic.Default });
        }

        foreach (var deprecate in overlay.DeprecateChoices)
        {
            var topic = Editable(topics, catalog, deprecate.Topic, "/deprecateChoices", issues);
            if (topic is null)
                continue;
            if (topic.Choices.All(c => c.Id != deprecate.Choice))
                issues.Add(new SpecIssue("/deprecateChoices", $"Choice '{deprecate.Choice}' does not exist for '{topic.Id}'."));
            else
                Replace(topics, topic with { Choices = topic.Choices.Select(c => c.Id == deprecate.Choice ? c with { Support = SpecSupport.Deprecated } : c).ToArray() });
        }

        foreach (var set in overlay.SetDefaults)
        {
            var topic = Editable(topics, catalog, set.Topic, "/setDefaults", issues);
            if (topic is null)
                continue;
            if (topic.Choices.All(c => c.Id != set.Choice))
                issues.Add(new SpecIssue("/setDefaults", $"Choice '{set.Choice}' does not exist for '{topic.Id}'."));
            else
                Replace(topics, topic with { Default = set.Choice });
        }

        foreach (var profile in overlay.AddProfiles)
        {
            if (overlay.Namespace is not null && !profile.Id.StartsWith(overlay.Namespace + ".", StringComparison.Ordinal))
            {
                issues.Add(new SpecIssue("/addProfiles", $"Profile '{profile.Id}' must start with '{overlay.Namespace}.'."));
                continue;
            }

            if (profiles.Any(p => p.Id == profile.Id))
            {
                issues.Add(new SpecIssue("/addProfiles", $"Profile '{profile.Id}' already exists."));
                continue;
            }

            foreach (var d in profile.Decisions)
            {
                var topic = topics.FirstOrDefault(t => t.Id == d.Topic);
                if (topic is null)
                {
                    issues.Add(new SpecIssue("/addProfiles", $"Profile '{profile.Id}' uses unknown topic '{d.Topic}'."));
                    continue;
                }

                var decision = new SpecDecision(d.Topic, overlay.Level, d.Choice, d.Params, Array.Empty<SpecConstraint>(), false, null);
                issues.AddRange(SpecDecisionValidator.Validate(topic, decision)
                    .Select(i => new SpecIssue($"/addProfiles/{profile.Id}{i.Path}", i.Message)));
            }

            profiles.Add(profile with { Source = source });
        }

        if (issues.Count == 0)
            merged = catalog with { Topics = topics, Profiles = profiles };
        return issues;
    }

    private static SpecTopic? Editable(List<SpecTopic> topics, SpecCatalog catalog, string topicId, string path, List<SpecIssue> issues)
    {
        var topic = topics.FirstOrDefault(t => t.Id == topicId);
        if (topic is null)
        {
            issues.Add(new SpecIssue(path, $"Topic '{topicId}' does not exist."));
            return null;
        }

        if (catalog.IsLocked(topicId))
        {
            issues.Add(new SpecIssue(path, $"Topic '{topicId}' is locked by the platform and cannot be changed."));
            return null;
        }

        return topic;
    }

    private static void Replace(List<SpecTopic> topics, SpecTopic updated) =>
        topics[topics.FindIndex(t => t.Id == updated.Id)] = updated;

    private static Version Parse(string version) =>
        Version.TryParse(version, out var v) ? v : new Version(0, 0);

    public static string Root(string id)
    {
        var dot = id.IndexOf('.', StringComparison.Ordinal);
        return dot < 0 ? id : id[..dot];
    }
}
