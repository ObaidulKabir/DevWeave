using DevWeave.Domain.Specifications;
using Xunit;

namespace DevWeave.Domain.Tests.Specifications;

public sealed class SpecCatalogTests
{
    private static readonly IReadOnlyList<SpecPack> Packs = SpecPackLoader.LoadBuiltIn();

    private static SpecCatalog Build(params string[] overlayJson) =>
        SpecCatalogBuilder.Build(
            Packs,
            overlayJson.Select((json, i) => (SpecOverlayJson.TryParse(json, out var e)
                ?? throw new InvalidOperationException(e), $"test:{i}")).ToList());

    [Fact]
    public void Built_in_packs_load_and_only_default_packs_are_enabled()
    {
        Assert.Contains(Packs, p => p.Id == "core");
        Assert.Contains(Packs, p => p.Id == "delivery" && !p.EnabledByDefault);
        Assert.NotNull(SpecCatalog.Default.Find("backend.language"));
        Assert.Null(SpecCatalog.Default.Find("branching.model"));
    }

    [Fact]
    public void Every_built_in_profile_is_valid_once_all_packs_are_enabled()
    {
        var catalog = Build("""{"scope":{"level":"tenant"},"enablePacks":["delivery"]}""");
        Assert.Empty(catalog.Problems);
        foreach (var profile in catalog.Profiles)
        {
            foreach (var d in profile.Decisions)
            {
                var topic = catalog.Find(d.Topic);
                Assert.NotNull(topic);
                var issues = SpecDecisionValidator.Validate(
                    topic!, new SpecDecision(d.Topic, SpecScopeLevel.Project, d.Choice, d.Params, [], false, null));
                Assert.True(issues.Count == 0, $"{profile.Id}/{d.Topic}: {issues.FirstOrDefault()?.Message}");
            }
        }
    }

    [Fact]
    public void Overlay_can_enable_an_optional_pack()
    {
        var catalog = Build("""{"scope":{"level":"tenant"},"enablePacks":["delivery"]}""");
        Assert.NotNull(catalog.Find("branching.model"));
        Assert.Contains(catalog.Packs, p => p.Id == "delivery" && p.Enabled);
    }

    [Fact]
    public void Overlay_adds_custom_topic_choice_and_profile_under_its_namespace()
    {
        var catalog = Build("""
            {"scope":{"level":"tenant"},"namespace":"acme",
             "addTopics":[{"id":"acme.release-window","title":"Release window","choices":[{"id":"weekday"},{"id":"weekend"}]}],
             "addChoices":[{"topic":"backend.framework","choice":{"id":"acme-kit","label":"Acme kit","parameters":[{"name":"version","kind":"string"}]}}],
             "addProfiles":[{"id":"acme.default","decisions":[{"topic":"acme.release-window","choice":"weekday"}]}]}
            """);
        Assert.Empty(catalog.Problems);
        Assert.Equal("tenant", catalog.Find("acme.release-window")!.Source);
        Assert.Contains(catalog.Find("backend.framework")!.Choices, c => c.Id == "acme-kit" && c.Source == "tenant");
        Assert.Contains(catalog.Profiles, p => p.Id == "acme.default");
    }

    [Theory]
    [InlineData("""{"scope":{"level":"tenant"},"namespace":"backend","addTopics":[{"id":"backend.x","title":"x","choices":[{"id":"a"}]}]}""", "reserved")]
    [InlineData("""{"scope":{"level":"tenant"},"namespace":"acme","addTopics":[{"id":"other.x","title":"x","choices":[{"id":"a"}]}]}""", "must start with")]
    [InlineData("""{"scope":{"level":"tenant"},"addTopics":[{"id":"acme.x","title":"x","choices":[{"id":"a"}]}]}""", "namespace")]
    [InlineData("""{"scope":{"level":"tenant"},"namespace":"acme","addTopics":[{"id":"acme.x","title":"x","choices":[]}]}""", "at least one choice")]
    public void Overlay_namespace_rules_are_enforced(string json, string fragment)
    {
        var catalog = Build(json);
        var problem = Assert.Single(catalog.Problems);
        Assert.Contains(problem.Issues, i => i.Message.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        Assert.Null(catalog.Find("acme.x"));
    }

    [Fact]
    public void Overlay_cannot_change_a_locked_topic()
    {
        var catalog = Build("""
            {"scope":{"level":"project"},
             "addChoices":[{"topic":"source.code-policy","choice":{"id":"mirror"}}],
             "deprecateChoices":[{"topic":"agent.human-approval","choice":"required"}]}
            """);
        var problem = Assert.Single(catalog.Problems);
        Assert.Equal(2, problem.Issues.Count(i => i.Message.Contains("locked")));
        Assert.DoesNotContain(catalog.Find("source.code-policy")!.Choices, c => c.Id == "mirror");
    }

    [Fact]
    public void Overlay_cannot_hide_the_platform_choice_but_can_deprecate_and_hide_others()
    {
        var bad = Build("""{"scope":{"level":"tenant"},"hideChoices":[{"topic":"db.engine","choice":"postgresql"}]}""");
        Assert.Single(bad.Problems);

        var good = Build("""
            {"scope":{"level":"tenant"},
             "hideChoices":[{"topic":"db.engine","choice":"mysql"}],
             "deprecateChoices":[{"topic":"api.style","choice":"graphql"}],
             "setDefaults":[{"topic":"api.style","choice":"grpc"}]}
            """);
        Assert.Empty(good.Problems);
        Assert.DoesNotContain(good.Find("db.engine")!.Choices, c => c.Id == "mysql");
        Assert.Equal("grpc", good.Find("api.style")!.Default);

        var deprecated = good.Find("api.style")!;
        var issues = SpecDecisionValidator.Validate(
            deprecated, new SpecDecision("api.style", SpecScopeLevel.Project, "graphql", new Dictionary<string, string>(), [], false, null));
        Assert.Contains(issues, i => i.Message.Contains("deprecated"));
    }

    [Fact]
    public void A_bad_overlay_is_skipped_whole_and_later_good_overlays_still_apply()
    {
        var catalog = Build(
            """{"scope":{"level":"tenant"},"hideChoices":[{"topic":"nope","choice":"x"}],"setDefaults":[{"topic":"api.style","choice":"grpc"}]}""",
            """{"scope":{"level":"project"},"setDefaults":[{"topic":"api.style","choice":"graphql"}]}""");
        Assert.Single(catalog.Problems);
        Assert.Equal("graphql", catalog.Find("api.style")!.Default);
    }

    [Fact]
    public void Overlay_with_unknown_package_is_reported()
    {
        var catalog = Build("""{"scope":{"level":"tenant"},"enablePacks":["missing"]}""");
        Assert.Contains(catalog.Problems.SelectMany(p => p.Issues), i => i.Message.Contains("missing"));
    }

    [Fact]
    public void Overlay_parser_rejects_platform_scope_and_bad_shapes()
    {
        Assert.Null(SpecOverlayJson.TryParse("""{"scope":{"level":"platform"}}""", out var error));
        Assert.NotNull(error);
        Assert.Null(SpecOverlayJson.TryParse("not json", out _));
        Assert.Null(SpecOverlayJson.TryParse("""{"scope":{"level":"tenant"},"addTopics":[{"id":"a.b","title":"t","choices":[{"id":"c","parameters":[{"name":"p","kind":"weird"}]}]}]}""", out _));
    }
}
