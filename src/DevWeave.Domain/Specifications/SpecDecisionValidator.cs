using System.Globalization;

namespace DevWeave.Domain.Specifications;

/// <summary>Checks a decision against its topic definition so only legal, typed answers can exist.</summary>
public static class SpecDecisionValidator
{
    public static IReadOnlyList<SpecIssue> Validate(SpecTopic topic, SpecDecision decision)
    {
        var issues = new List<SpecIssue>();
        if (!string.Equals(topic.Id, decision.Topic, StringComparison.Ordinal))
            issues.Add(new SpecIssue("/topic", $"Decision is for '{decision.Topic}', not '{topic.Id}'."));

        var choice = topic.Choices.FirstOrDefault(c => c.Id == decision.Choice);
        if (choice is null)
        {
            issues.Add(new SpecIssue(
                "/decision/choice",
                $"'{decision.Choice}' is not a legal choice. Allowed: {string.Join(", ", topic.Choices.Select(c => c.Id))}."));
            return issues;
        }

        if (choice.Support == SpecSupport.Deprecated)
            issues.Add(new SpecIssue("/decision/choice", $"'{choice.Id}' is deprecated and cannot be chosen for new decisions."));

        foreach (var parameter in choice.ParameterList)
        {
            if (!decision.Params.TryGetValue(parameter.Name, out var value))
            {
                if (parameter.Required)
                    issues.Add(new SpecIssue($"/decision/params/{parameter.Name}", "Required parameter is missing."));
                continue;
            }

            if (!MatchesKind(parameter.Kind, value))
                issues.Add(new SpecIssue($"/decision/params/{parameter.Name}", $"Expected {parameter.Kind.ToString().ToLowerInvariant()}."));
            else if (parameter.AllowedValues is { Count: > 0 } allowed && !allowed.Contains(value))
                issues.Add(new SpecIssue($"/decision/params/{parameter.Name}", $"Must be one of: {string.Join(", ", allowed)}."));
        }

        foreach (var name in decision.Params.Keys.Where(k => choice.ParameterList.All(p => p.Name != k)))
            issues.Add(new SpecIssue($"/decision/params/{name}", "Parameter is not defined for this choice."));

        foreach (var constraint in decision.Constraints.Where(c => !topic.ConstraintRuleList.Contains(c.Rule)))
            issues.Add(new SpecIssue("/constraints", $"Constraint rule '{constraint.Rule}' is not defined for this topic."));

        return issues;
    }

    private static bool MatchesKind(SpecValueKind kind, string value) => kind switch
    {
        SpecValueKind.Integer => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
        SpecValueKind.Boolean => value is "true" or "false",
        _ => true,
    };
}
