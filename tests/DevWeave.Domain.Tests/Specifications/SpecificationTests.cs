using DevWeave.Domain.Specifications;
using Xunit;

namespace DevWeave.Domain.Tests.Specifications;

public sealed class SpecificationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

    private static SpecCandidate Candidate(
        string topic, SpecScopeLevel level, string choice, DateTimeOffset at,
        bool locked = false, bool predefined = false, params (string, string)[] p) =>
        new(
            new SpecDecision(topic, level, choice, p.ToDictionary(x => x.Item1, x => x.Item2),
                Array.Empty<SpecConstraint>(), locked, null),
            predefined ? null : Guid.NewGuid(), null, null, null, at);

    [Fact]
    public void Catalog_predefined_decisions_are_all_valid_against_their_topic()
    {
        foreach (var candidate in SpecTopicCatalog.PredefinedDecisions)
        {
            var topic = SpecTopicCatalog.Find(candidate.Decision.Topic);
            Assert.NotNull(topic);
            Assert.Empty(SpecDecisionValidator.Validate(topic!, candidate.Decision));
        }
    }

    [Fact]
    public void Catalog_predefined_stack_passes_cross_topic_rules()
    {
        var resolutions = SpecTopicCatalog.Topics.ToDictionary(
            t => t.Id, t => SpecResolver.Resolve(t.Id, SpecTopicCatalog.PredefinedDecisions));
        Assert.Empty(SpecResolver.CheckCompatibility(SpecTopicCatalog.Topics, resolutions));
    }

    [Fact]
    public void Validator_rejects_unknown_choice_missing_and_mistyped_parameters()
    {
        var topic = SpecTopicCatalog.Find("db.engine")!;

        var unknown = new SpecDecision("db.engine", SpecScopeLevel.Project, "oracle",
            new Dictionary<string, string>(), [], false, null);
        Assert.Contains(SpecDecisionValidator.Validate(topic, unknown), i => i.Path == "/decision/choice");

        var missing = new SpecDecision("db.engine", SpecScopeLevel.Project, "postgresql",
            new Dictionary<string, string>(), [], false, null);
        Assert.Contains(SpecDecisionValidator.Validate(topic, missing), i => i.Path == "/decision/params/majorVersion");

        var notAllowed = new SpecDecision("db.engine", SpecScopeLevel.Project, "postgresql",
            new Dictionary<string, string> { ["majorVersion"] = "9" }, [], false, null);
        Assert.Contains(SpecDecisionValidator.Validate(topic, notAllowed), i => i.Message.StartsWith("Must be one of"));

        var extra = new SpecDecision("db.engine", SpecScopeLevel.Project, "postgresql",
            new Dictionary<string, string> { ["majorVersion"] = "17", ["flavour"] = "x" }, [], false, null);
        Assert.Contains(SpecDecisionValidator.Validate(topic, extra), i => i.Path == "/decision/params/flavour");
    }

    [Fact]
    public void Parser_normalises_scalars_and_rejects_malformed_content()
    {
        var decision = SpecDecisionJson.TryParse("""
            {"topic":"db.access","scope":{"level":"project"},
             "decision":{"choice":"ef-core","params":{"migrations":true}},"locked":false}
            """, out _);
        Assert.NotNull(decision);
        Assert.Equal("true", decision!.Params["migrations"]);
        Assert.Equal(SpecScopeLevel.Project, decision.Level);

        Assert.Null(SpecDecisionJson.TryParse("{\"topic\":1}", out var error));
        Assert.NotNull(error);
        Assert.Null(SpecDecisionJson.TryParse("not json", out _));
    }

    [Fact]
    public void Resolver_returns_the_predefined_decision_when_nothing_else_exists()
    {
        var result = SpecResolver.Resolve("backend.language", SpecTopicCatalog.PredefinedDecisions);
        Assert.Equal(SpecOrigin.Predefined, result.Origin);
        Assert.Equal("csharp", result.Binding!.Decision.Choice);
    }

    [Fact]
    public void Resolver_first_approved_wins_within_a_level_and_later_differences_conflict()
    {
        var first = Candidate("t", SpecScopeLevel.Project, "a", T0);
        var later = Candidate("t", SpecScopeLevel.Project, "b", T0.AddDays(1));
        var same = Candidate("t", SpecScopeLevel.Project, "a", T0.AddDays(2));

        var result = SpecResolver.Resolve("t", [later, same, first]);

        Assert.Equal("a", result.Binding!.Decision.Choice);
        Assert.Equal(SpecOrigin.FirstApproved, result.Origin);
        var conflict = Assert.Single(result.Conflicts);
        Assert.Equal("b", conflict.Other.Decision.Choice);
    }

    [Fact]
    public void Resolver_narrower_scope_overrides_unlocked_wider_decision()
    {
        var platform = Candidate("t", SpecScopeLevel.Platform, "a", T0, predefined: true);
        var project = Candidate("t", SpecScopeLevel.Project, "b", T0.AddDays(5));

        var result = SpecResolver.Resolve("t", [platform, project]);

        Assert.Equal("b", result.Binding!.Decision.Choice);
        Assert.Equal(SpecOrigin.Override, result.Origin);
        Assert.Empty(result.Conflicts);
    }

    [Fact]
    public void Resolver_locked_wider_decision_cannot_be_overridden()
    {
        var platform = Candidate("t", SpecScopeLevel.Platform, "a", T0, locked: true, predefined: true);
        var project = Candidate("t", SpecScopeLevel.Project, "b", T0.AddDays(5));

        var result = SpecResolver.Resolve("t", [platform, project]);

        Assert.Equal("a", result.Binding!.Decision.Choice);
        Assert.Single(result.Conflicts);
    }

    [Fact]
    public void Compatibility_flags_a_choice_that_breaks_a_cross_topic_rule()
    {
        var candidates = new[]
        {
            Candidate("backend.language", SpecScopeLevel.Project, "java", T0, p: ("version", "21")),
            Candidate("backend.framework", SpecScopeLevel.Project, "aspnet-core", T0, p: ("targetFramework", "net10.0")),
        };
        var resolutions = SpecTopicCatalog.Topics.ToDictionary(t => t.Id, t => SpecResolver.Resolve(t.Id, candidates));

        var issues = SpecResolver.CheckCompatibility(SpecTopicCatalog.Topics, resolutions);

        Assert.Contains(issues, i => i.Path == "backend.framework");
    }
}
