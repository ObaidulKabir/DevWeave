using DevWeave.Domain.Specifications;

namespace DevWeave.Application.Specifications;

/// <summary>Stable JSON-friendly shapes shared by the REST and MCP surfaces.</summary>
public static class SpecProjection
{
    public static object Choice(SpecChoice c) => new
    {
        c.Id,
        label = c.Label ?? c.Id,
        support = c.Support.ToString().ToLowerInvariant(),
        c.Source,
        custom = c.Source is "tenant" or "project",
        parameters = c.ParameterList.Select(p => new
        {
            p.Name,
            kind = p.Kind.ToString().ToLowerInvariant(),
            p.Required,
            allowedValues = p.AllowedValues ?? Array.Empty<string>(),
        }),
    };

    public static object Topic(SpecTopic topic) => new
    {
        topic.Id,
        topic.Title,
        topic.Description,
        topic.Domain,
        topic.AllowCustom,
        topic.Default,
        topic.Source,
        choices = topic.Choices.Select(Choice),
        constraintRules = topic.ConstraintRuleList,
        rules = topic.RuleList.Select(r => new { r.WhenChoice, r.OtherTopic, r.AllowedOtherChoices }),
    };

    public static object Profile(SpecProfile p) => new
    {
        p.Id,
        p.Label,
        p.Description,
        p.ProjectType,
        p.Source,
        decisions = p.Decisions.Select(d => new { d.Topic, d.Choice, @params = d.Params }),
    };

    public static object Catalog(SpecCatalog c) => new
    {
        packs = c.Packs.Select(p => new { p.Id, p.Version, p.Description, p.Enabled, p.Required }),
        topics = c.Topics.Select(Topic),
        profiles = c.Profiles.Select(Profile),
        problems = c.Problems.Select(p => new { p.Source, issues = p.Issues.Select(i => new { i.Path, i.Message }) }),
    };

    public static object Options((SpecTopic Topic, IReadOnlyList<SpecOptionView> Options) result) => new
    {
        topic = result.Topic.Id,
        options = result.Options.Select(o => new
        {
            choice = Choice(o.Choice),
            o.Recommended,
            o.Compatible,
            o.Reasons,
        }),
    };

    public static object OpenTopics(IReadOnlyList<OpenTopicView> topics) =>
        topics.Select(t => new { topic = Topic(t.Topic), t.Default, t.DependedOnBy });

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
        catalogProblems = e.CatalogProblems.Select(p => new { p.Source, issues = p.Issues.Select(i => new { i.Path, i.Message }) }),
    };
}
