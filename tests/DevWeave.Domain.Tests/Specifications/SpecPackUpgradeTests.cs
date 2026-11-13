using DevWeave.Domain.Specifications;
using Xunit;

namespace DevWeave.Domain.Tests.Specifications;

public sealed class SpecPackUpgradeTests
{
    private const string V1 = """
        {"id":"demo","version":"1.0.0","topics":[
          {"id":"demo.db","title":"DB","default":"pg","choices":[{"id":"pg"},{"id":"legacy"}]},
          {"id":"demo.old","title":"Old","choices":[{"id":"a"}]}],
         "decisions":[{"topic":"demo.db","choice":"pg"}],
         "profiles":[{"id":"p1","decisions":[{"topic":"demo.db","choice":"pg"}]}]}
        """;

    private const string V2 = """
        {"id":"demo","version":"1.1.0","topics":[
          {"id":"demo.db","title":"DB","default":"newdb","choices":[{"id":"pg"},{"id":"legacy","support":"deprecated"},{"id":"newdb"}]},
          {"id":"demo.cache","title":"Cache","choices":[{"id":"redis"}]}],
         "decisions":[{"topic":"demo.db","choice":"newdb"}],
         "profiles":[{"id":"p2","decisions":[{"topic":"demo.cache","choice":"redis"}]}]}
        """;

    private static readonly SpecPack Pack1 = SpecPackLoader.Parse(V1);
    private static readonly SpecPack Pack2 = SpecPackLoader.Parse(V2);

    private static SpecCatalog Build(params string[] overlays) =>
        SpecCatalogBuilder.Build(
            [Pack1, Pack2],
            overlays.Select((json, i) => (SpecOverlayJson.TryParse(json, out var e) ?? throw new InvalidOperationException(e), $"o{i}")).ToList());

    [Fact]
    public void Newest_version_is_used_unless_a_pin_holds_the_pack_back()
    {
        var latest = Build();
        Assert.Equal("1.1.0", latest.Packs.Single().Version);
        Assert.NotNull(latest.Find("demo.cache"));
        Assert.False(latest.Packs.Single().UpdateAvailable);

        var pinned = Build("""{"scope":{"level":"tenant"},"pinPacks":[{"pack":"demo","version":"1.0.0"}]}""");
        var status = pinned.Packs.Single();
        Assert.Equal("1.0.0", status.Version);
        Assert.True(status.Pinned);
        Assert.True(status.UpdateAvailable);
        Assert.Equal("1.1.0", status.LatestVersion);
        Assert.Null(pinned.Find("demo.cache"));
        Assert.NotNull(pinned.Find("demo.old"));
    }

    [Fact]
    public void Pin_to_an_unknown_version_is_reported_and_ignored()
    {
        var catalog = Build("""{"scope":{"level":"tenant"},"pinPacks":[{"pack":"demo","version":"9.9.9"}]}""");
        Assert.Equal("1.1.0", catalog.Packs.Single().Version);
        Assert.Contains(catalog.Problems.SelectMany(p => p.Issues), i => i.Message.Contains("9.9.9"));
    }

    [Fact]
    public void Diff_lists_added_removed_deprecated_default_decision_and_profile_changes()
    {
        var diff = SpecPackDiff.Compare(Pack1, Pack2);

        Assert.Equal("1.0.0", diff.From);
        Assert.Equal("1.1.0", diff.To);
        Assert.Equal(["demo.cache"], diff.TopicsAdded);
        Assert.Equal(["demo.old"], diff.TopicsRemoved);
        Assert.Contains(diff.ChoiceChanges, c => c is { Topic: "demo.db", Choice: "newdb", Change: "added" });
        Assert.Contains(diff.ChoiceChanges, c => c is { Topic: "demo.db", Choice: "legacy", Change: "deprecated" });
        Assert.Contains(diff.DefaultChanges, d => d is { Topic: "demo.db", From: "pg", To: "newdb" });
        Assert.Contains(diff.DecisionChanges, d => d is { Topic: "demo.db", FromChoice: "pg", ToChoice: "newdb" });
        Assert.Equal(["p2"], diff.ProfilesAdded);
        Assert.Equal(["p1"], diff.ProfilesRemoved);
        Assert.False(diff.IsEmpty);
        Assert.True(SpecPackDiff.Compare(Pack1, Pack1).IsEmpty);
    }

    [Fact]
    public void Bundle_round_trips_and_rejects_platform_scope()
    {
        var bundle = new SpecBundle(
            "dotnet-postgres-react", SpecScopeLevel.Project,
            [new SpecProfileDecision("db.engine", "postgresql", new Dictionary<string, string> { ["majorVersion"] = "17" }),
             new SpecProfileDecision("db.access", "ef-core", new Dictionary<string, string> { ["migrations"] = "true" })],
            "Standard stack");

        var parsed = SpecBundleJson.TryParse(SpecBundleJson.ToJson(bundle), out var error);

        Assert.NotNull(parsed);
        Assert.Null(error);
        Assert.Equal("dotnet-postgres-react", parsed!.ProfileId);
        Assert.Equal(2, parsed.Decisions.Count);
        Assert.Equal("17", parsed.Decisions[0].Params["majorVersion"]);
        Assert.Equal("true", parsed.Decisions[1].Params["migrations"]);
        Assert.Null(SpecBundleJson.TryParse("""{"scope":{"level":"platform"},"decisions":[]}""", out _));
    }
}
