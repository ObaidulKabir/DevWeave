using DevWeave.Domain.Specifications;

namespace DevWeave.Application.Specifications;

/// <summary>Stable JSON-friendly shapes shared by the REST and MCP surfaces.</summary>
public static class SpecProjection
{
    public static object Topic(SpecTopic topic) => new
    {
        topic.Id,
        topic.Title,
        topic.Description,
        choices = topic.Choices.Select(c => new
        {
            c.Id,
            parameters = c.ParameterList.Select(p => new
            {
                p.Name,
                kind = p.Kind.ToString().ToLowerInvariant(),
                p.Required,
                allowedValues = p.AllowedValues ?? Array.Empty<string>(),
            }),
        }),
        constraintRules = topic.ConstraintRuleList,
        rules = topic.RuleList.Select(r => new { r.WhenChoice, r.OtherTopic, r.AllowedOtherChoices }),
    };

    public static object Candidate(SpecCandidate c) => new
    {
        topic = c.Decision.Topic,
        scope = SpecDecisionJson.ToLevelString(c.Decision.Level),
        choice = c.Decision.Choice,
        @params = c.Decision.Params,
        constraints = c.Decision.Constraints.Select(x => new { x.Rule, x.Value }),
        locked = c.Decision.Locked,
        itemId = c.ItemId,
        revisionId = c.RevisionId,
        revisionNumber = c.RevisionNumber,
        definedAt = c.DefinedAt,
    };

    public static object Resolution(SpecResolution r) => new
    {
        topic = r.Topic,
        decided = r.Binding is not null,
        origin = r.Origin?.ToString().ToLowerInvariant(),
        binding = r.Binding is null ? null : Candidate(r.Binding),
        conflicts = r.Conflicts.Select(c => new
        {
            reason = c.Reason,
            binding = Candidate(c.Binding),
            other = Candidate(c.Other),
        }),
    };

    public static object Effective(EffectiveSpecification e) => new
    {
        decisions = e.Resolutions.Select(Resolution),
        incompatibilities = e.Incompatibilities.Select(i => new { i.Path, i.Message }),
        undecidedTopics = e.UndecidedTopics,
    };
}
