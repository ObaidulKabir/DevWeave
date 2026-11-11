namespace DevWeave.Domain.Specifications;

/// <summary>
/// Computes the binding decision for a topic. Precedence, in order:
/// narrower scope wins (project over tenant over platform) unless a wider decision is locked;
/// within one scope level the earliest-defined decision wins ("first one wins") and any later decision
/// with different content is reported as a conflict. Predefined platform decisions are ordinary
/// candidates at platform level, so a design-time decision always precedes a later one at that level.
/// </summary>
public static class SpecResolver
{
    private static readonly SpecScopeLevel[] WidestFirst =
        [SpecScopeLevel.Platform, SpecScopeLevel.Tenant, SpecScopeLevel.Project];

    public static SpecResolution Resolve(string topic, IEnumerable<SpecCandidate> candidates)
    {
        var all = candidates.Where(c => c.Decision.Topic == topic).ToList();
        if (all.Count == 0)
            return new SpecResolution(topic, null, null, Array.Empty<SpecConflict>());

        var conflicts = new List<SpecConflict>();

        // Per level: the earliest decision wins; later differing decisions conflict with it.
        var perLevel = new Dictionary<SpecScopeLevel, SpecCandidate>();
        foreach (var group in all.GroupBy(c => c.Decision.Level))
        {
            var ordered = group.OrderBy(c => c.DefinedAt).ThenBy(c => c.ItemId).ToList();
            var winner = ordered[0];
            perLevel[group.Key] = winner;
            foreach (var other in ordered.Skip(1).Where(o => !SameDecision(winner.Decision, o.Decision)))
                conflicts.Add(new SpecConflict(winner, other, "Two approved decisions disagree at the same scope level."));
        }

        // Walk from the widest level inward; a locked wider decision cannot be overridden.
        SpecCandidate? binding = null;
        var origin = SpecOrigin.Predefined;
        foreach (var level in WidestFirst)
        {
            if (!perLevel.TryGetValue(level, out var candidate))
                continue;

            if (binding is null)
            {
                binding = candidate;
                origin = candidate.ItemId is null ? SpecOrigin.Predefined : SpecOrigin.FirstApproved;
            }
            else if (binding.Decision.Locked)
            {
                conflicts.Add(new SpecConflict(binding, candidate, "A locked wider decision cannot be overridden."));
            }
            else if (!SameDecision(binding.Decision, candidate.Decision))
            {
                binding = candidate;
                origin = SpecOrigin.Override;
            }
        }

        return new SpecResolution(topic, binding, origin, conflicts);
    }

    /// <summary>Checks cross-topic rules over a set of resolutions and returns the violated ones.</summary>
    public static IReadOnlyList<SpecIssue> CheckCompatibility(
        IReadOnlyCollection<SpecTopic> topics,
        IReadOnlyDictionary<string, SpecResolution> resolutions)
    {
        var issues = new List<SpecIssue>();
        foreach (var topic in topics)
        {
            if (!resolutions.TryGetValue(topic.Id, out var mine) || mine.Binding is null)
                continue;

            foreach (var rule in topic.RuleList.Where(r => r.WhenChoice == mine.Binding.Decision.Choice))
            {
                if (resolutions.TryGetValue(rule.OtherTopic, out var other) && other.Binding is not null &&
                    !rule.AllowedOtherChoices.Contains(other.Binding.Decision.Choice))
                {
                    issues.Add(new SpecIssue(
                        topic.Id,
                        $"'{topic.Id}={rule.WhenChoice}' requires '{rule.OtherTopic}' to be one of " +
                        $"[{string.Join(", ", rule.AllowedOtherChoices)}], but it is '{other.Binding.Decision.Choice}'."));
                }
            }
        }

        return issues;
    }

    private static bool SameDecision(SpecDecision a, SpecDecision b) =>
        a.Choice == b.Choice &&
        a.Params.Count == b.Params.Count &&
        a.Params.All(p => b.Params.TryGetValue(p.Key, out var v) && v == p.Value);
}
