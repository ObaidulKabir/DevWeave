namespace DevWeave.Domain.Specifications;

public sealed record SpecChoiceChange(string Topic, string Choice, string Change);

public sealed record SpecDefaultChange(string Topic, string? From, string? To);

public sealed record SpecDecisionChange(string Topic, string? FromChoice, string? ToChoice, bool LockChanged);

/// <summary>What changes for a tenant that moves from one version of a pack to another.</summary>
public sealed record SpecPackDiff(
    string PackId,
    string From,
    string To,
    IReadOnlyList<string> TopicsAdded,
    IReadOnlyList<string> TopicsRemoved,
    IReadOnlyList<SpecChoiceChange> ChoiceChanges,
    IReadOnlyList<SpecDefaultChange> DefaultChanges,
    IReadOnlyList<SpecDecisionChange> DecisionChanges,
    IReadOnlyList<string> ProfilesAdded,
    IReadOnlyList<string> ProfilesRemoved)
{
    public bool IsEmpty =>
        TopicsAdded.Count + TopicsRemoved.Count + ChoiceChanges.Count + DefaultChanges.Count +
        DecisionChanges.Count + ProfilesAdded.Count + ProfilesRemoved.Count == 0;

    public static SpecPackDiff Compare(SpecPack from, SpecPack to)
    {
        var added = to.Topics.Where(t => from.Topics.All(f => f.Id != t.Id)).Select(t => t.Id).ToArray();
        var removed = from.Topics.Where(f => to.Topics.All(t => t.Id != f.Id)).Select(t => t.Id).ToArray();

        var choices = new List<SpecChoiceChange>();
        var defaults = new List<SpecDefaultChange>();
        foreach (var next in to.Topics)
        {
            var prev = from.Topics.FirstOrDefault(t => t.Id == next.Id);
            if (prev is null)
                continue;

            foreach (var c in next.Choices.Where(c => prev.Choices.All(p => p.Id != c.Id)))
                choices.Add(new SpecChoiceChange(next.Id, c.Id, "added"));
            foreach (var c in prev.Choices.Where(c => next.Choices.All(n => n.Id != c.Id)))
                choices.Add(new SpecChoiceChange(next.Id, c.Id, "removed"));
            foreach (var c in next.Choices)
            {
                var before = prev.Choices.FirstOrDefault(p => p.Id == c.Id);
                if (before is null || before.Support == c.Support)
                    continue;
                choices.Add(new SpecChoiceChange(next.Id, c.Id,
                    c.Support == SpecSupport.Deprecated ? "deprecated" : $"support changed to {c.Support.ToString().ToLowerInvariant()}"));
            }

            if (prev.Default != next.Default)
                defaults.Add(new SpecDefaultChange(next.Id, prev.Default, next.Default));
        }

        var decisions = new List<SpecDecisionChange>();
        var topicsWithDecisions = from.Decisions.Select(d => d.Decision.Topic).Concat(to.Decisions.Select(d => d.Decision.Topic)).Distinct();
        foreach (var topic in topicsWithDecisions)
        {
            var a = from.Decisions.FirstOrDefault(d => d.Decision.Topic == topic)?.Decision;
            var b = to.Decisions.FirstOrDefault(d => d.Decision.Topic == topic)?.Decision;
            var paramsDiffer = a is not null && b is not null &&
                !(a.Params.Count == b.Params.Count && a.Params.All(p => b.Params.TryGetValue(p.Key, out var v) && v == p.Value));
            if (a?.Choice != b?.Choice || paramsDiffer || a?.Locked != b?.Locked)
                decisions.Add(new SpecDecisionChange(topic, a?.Choice, b?.Choice, a?.Locked != b?.Locked));
        }

        return new SpecPackDiff(
            to.Id, from.Version, to.Version, added, removed, choices, defaults, decisions,
            to.Profiles.Where(p => from.Profiles.All(f => f.Id != p.Id)).Select(p => p.Id).ToArray(),
            from.Profiles.Where(p => to.Profiles.All(t => t.Id != p.Id)).Select(p => p.Id).ToArray());
    }
}
