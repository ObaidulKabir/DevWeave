using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DevWeave.Application.Abstractions;
using DevWeave.Contracts.Planned.V1;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Knowledge;
using DevWeave.Mcp.Protocol;
using DevWeave.Application.Specifications;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Specifications;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DevWeave.Api.IntegrationTests;

public sealed class SpecificationIntegrationTests : IClassFixture<IdentityTestFactory>
{
    private readonly IdentityTestFactory _factory;

    public SpecificationIntegrationTests(IdentityTestFactory factory)
    {
        _factory = factory;
        _factory.PrepareDatabase();
    }

    private const string Content = """
        {"topic":"db.engine","scope":{"level":"project"},
         "decision":{"choice":"postgresql","params":{"majorVersion":17}},
         "rationale":"Matches the hosting plan."}
        """;

    [Fact]
    public async Task Proposed_decision_binds_only_after_human_approval_and_is_resolved_as_data()
    {
        var seed = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var specs = scope.ServiceProvider.GetRequiredService<SpecificationService>();

        var before = await specs.ResolveAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, "db.engine");
        Assert.Equal(SpecOrigin.Predefined, before.Origin);
        Assert.Equal("18", before.Binding!.Decision.Params["majorVersion"]);

        var item = await specs.ProposeAsync(
            seed.Tenant.Id, seed.Project.Id, seed.Member.Id, "db.engine", SpecScopeLevel.Project, Content, "Hosting plan");

        // Unreviewed: still the predefined answer.
        var pending = await specs.ResolveAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, "db.engine");
        Assert.Equal(SpecOrigin.Predefined, pending.Origin);

        using (var approval = _factory.Services.CreateScope())
        {
            var db = approval.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
            (await db.KnowledgeItems.SingleAsync(i => i.Id == item.Id)).Approve();
            await db.SaveChangesAsync();
        }

        using var fresh = _factory.Services.CreateScope();
        var after = await fresh.ServiceProvider.GetRequiredService<SpecificationService>()
            .ResolveAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, "db.engine");
        Assert.Equal(SpecOrigin.Override, after.Origin);
        Assert.Equal("17", after.Binding!.Decision.Params["majorVersion"]);
        Assert.Equal(item.Id.Value, after.Binding.ItemId);
        Assert.Equal(1, after.Binding.RevisionNumber);
    }

    [Fact]
    public async Task Illegal_decisions_and_locked_topics_are_rejected_before_storage()
    {
        var seed = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var specs = scope.ServiceProvider.GetRequiredService<SpecificationService>();

        var illegal = Content.Replace("postgresql", "oracle");
        var rejected = await Assert.ThrowsAsync<SpecDecisionRejectedException>(() =>
            specs.ProposeAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, "db.engine", SpecScopeLevel.Project, illegal, ""));
        Assert.Contains(rejected.Issues, i => i.Path == "/decision/choice");

        var locked = """{"topic":"source.code-policy","scope":{"level":"project"},"decision":{"choice":"store-source"}}""";
        await Assert.ThrowsAsync<ConflictException>(() =>
            specs.ProposeAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, "source.code-policy", SpecScopeLevel.Project, locked, ""));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            specs.ProposeAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, "no.such.topic", SpecScopeLevel.Project, Content, ""));
    }

    [Fact]
    public async Task Rest_surface_lists_topics_effective_spec_and_rejects_invalid_proposals()
    {
        var seed = await SeedAsync();
        using var client = _factory.CreateAuthenticatedHumanClient(seed.Tenant.Id.Value, seed.Owner.Id.Value);
        var root = $"/tenants/{seed.Tenant.Id.Value}";

        using var topics = await client.GetAsync($"{root}/spec/topics");
        Assert.Equal(HttpStatusCode.OK, topics.StatusCode);
        using var topicDoc = await topics.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.True(topicDoc!.RootElement.GetArrayLength() >= 8);

        using var effective = await client.GetAsync($"{root}/projects/{seed.Project.Id.Value}/spec/effective");
        Assert.Equal(HttpStatusCode.OK, effective.StatusCode);
        using var effectiveDoc = await effective.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(0, effectiveDoc!.RootElement.GetProperty("undecidedTopics").GetArrayLength());
        Assert.Equal(0, effectiveDoc.RootElement.GetProperty("incompatibilities").GetArrayLength());

        using var bad = await client.PostAsJsonAsync(
            $"{root}/projects/{seed.Project.Id.Value}/spec/decisions",
            new
            {
                topicId = "db.engine",
                level = "project",
                decision = new { topic = "db.engine", scope = new { level = "project" }, decision = new { choice = "oracle" } },
                reason = "x",
            });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, bad.StatusCode);

        using var good = await client.PostAsJsonAsync(
            $"{root}/projects/{seed.Project.Id.Value}/spec/decisions",
            new
            {
                topicId = "db.engine",
                level = "project",
                decision = JsonDocument.Parse(Content).RootElement,
                reason = "Hosting plan",
            });
        Assert.Equal(HttpStatusCode.Created, good.StatusCode);
    }

    [Fact]
    public async Task Agent_resolves_and_proposes_specifications_over_mcp_but_cannot_bind_them()
    {
        var seed = await SeedAsync();
        Principal agent;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
            agent = Principal.Create(seed.Tenant.Id, $"spec-{Guid.NewGuid():N}@example.test", PrincipalType.AiAgent);
            db.Add(agent);
            db.Add(ProjectMembership.Grant(seed.Tenant.Id, seed.Project.Id, agent.Id, ProjectRole.Member));
            db.Add(AgentCredential.Issue(seed.Tenant.Id, agent.Id, "test", "hashed-test-secret", null));
            foreach (var capability in new[] { AgentCapability.ReadKnowledge, AgentCapability.WriteKnowledge })
            {
                var grant = AgentGrant.Propose(seed.Tenant.Id, seed.Project.Id, agent.Id, capability, null, Classification.Internal);
                grant.Approve(seed.Owner.Id);
                db.Add(grant);
            }

            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateAuthenticatedAgentClient(seed.Tenant.Id.Value, agent.Id.Value);
        var url = $"/tenants/{seed.Tenant.Id.Value}/projects/{seed.Project.Id.Value}/mcp/rpc";

        async Task<JsonDocument> CallAsync(int id, string tool, object arguments)
        {
            using var response = await client.PostAsJsonAsync(
                url, new JsonRpcRequest("2.0", id, McpMethods.CallTool, new { name = tool, arguments }));
            return (await response.Content.ReadFromJsonAsync<JsonDocument>())!;
        }

        using var tools = await client.PostAsJsonAsync(url, new JsonRpcRequest("2.0", 1, McpMethods.ListTools, new { }));
        var listed = await tools.Content.ReadAsStringAsync();
        Assert.Contains("resolve_spec", listed);
        Assert.Contains("propose_spec_decision", listed);

        using var resolved = await CallAsync(2, "resolve_spec", new { topic = "backend.language" });
        Assert.Equal(JsonValueKind.Null, resolved.RootElement.GetProperty("error").ValueKind);
        var binding = resolved.RootElement.GetProperty("result").GetProperty("binding");
        Assert.Equal("csharp", binding.GetProperty("choice").GetString());

        using var effective = await CallAsync(3, "get_effective_spec", new { });
        Assert.Equal(0, effective.RootElement.GetProperty("result").GetProperty("undecidedTopics").GetArrayLength());

        using var bad = await CallAsync(4, "propose_spec_decision",
            new { topic = "db.engine", level = "project", choice = "oracle" });
        Assert.NotEqual(JsonValueKind.Null, bad.RootElement.GetProperty("error").ValueKind);

        using var good = await CallAsync(5, "propose_spec_decision",
            new { topic = "db.engine", level = "project", choice = "postgresql", @params = new { majorVersion = 17 } });
        Assert.Equal(JsonValueKind.Null, good.RootElement.GetProperty("error").ValueKind);

        // A proposal from an agent never binds until a human approves it.
        using var after = await CallAsync(6, "resolve_spec", new { topic = "db.engine" });
        Assert.Equal("predefined", after.RootElement.GetProperty("result").GetProperty("origin").GetString());
    }

    private const string Overlay = """
        {"scope":{"level":"project"},"namespace":"acme",
         "addTopics":[{"id":"acme.release-window","title":"Release window","domain":"delivery",
           "choices":[{"id":"weekday","support":"recommended"},{"id":"weekend"}]}],
         "enablePacks":["delivery"]}
        """;

    [Fact]
    public async Task Approved_overlay_extends_the_catalog_and_unapproved_one_does_not()
    {
        var seed = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var specs = scope.ServiceProvider.GetRequiredService<SpecificationService>();

        var before = await specs.CatalogAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id);
        Assert.Null(before.Find("acme.release-window"));
        Assert.Null(before.Find("branching.model"));

        var item = await specs.ProposeOverlayAsync(seed.Tenant.Id, seed.Project.Id, seed.Member.Id, Overlay, "Our release rules");

        // Proposed only: the catalog is unchanged.
        var pending = await specs.CatalogAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id);
        Assert.Null(pending.Find("acme.release-window"));

        using (var approval = _factory.Services.CreateScope())
        {
            var db = approval.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
            (await db.KnowledgeItems.SingleAsync(i => i.Id == item.Id)).Approve();
            await db.SaveChangesAsync();
        }

        using var fresh = _factory.Services.CreateScope();
        var service = fresh.ServiceProvider.GetRequiredService<SpecificationService>();
        var after = await service.CatalogAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id);
        Assert.Equal("project", after.Find("acme.release-window")!.Source);
        Assert.NotNull(after.Find("branching.model"));
        Assert.Empty(after.Problems);

        // The new topic is open until someone decides it, and shows first-class in the open-topics queue.
        var open = await service.OpenTopicsAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id);
        Assert.Contains(open, o => o.Topic.Id == "acme.release-window");

        // And decisions on it validate against the custom definition.
        var decision = """{"topic":"acme.release-window","scope":{"level":"project"},"decision":{"choice":"weekday"}}""";
        var proposed = await service.ProposeAsync(
            seed.Tenant.Id, seed.Project.Id, seed.Member.Id, "acme.release-window", SpecScopeLevel.Project, decision, "");
        Assert.NotNull(proposed);

        // Another project is not affected by a project-level overlay.
        var other = await SeedAsync();
        var otherCatalog = await service.CatalogAsync(other.Tenant.Id, other.Project.Id, other.Owner.Id);
        Assert.Null(otherCatalog.Find("acme.release-window"));
    }

    [Fact]
    public async Task Invalid_overlays_are_rejected_before_storage()
    {
        var seed = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var specs = scope.ServiceProvider.GetRequiredService<SpecificationService>();

        var reserved = Overlay.Replace("acme", "backend");
        var rejected = await Assert.ThrowsAsync<SpecDecisionRejectedException>(() =>
            specs.ProposeOverlayAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, reserved, ""));
        Assert.Contains(rejected.Issues, i => i.Message.Contains("reserved"));

        var locked = """{"scope":{"level":"project"},"deprecateChoices":[{"topic":"source.code-policy","choice":"references-only"}]}""";
        var lockedRejected = await Assert.ThrowsAsync<SpecDecisionRejectedException>(() =>
            specs.ProposeOverlayAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, locked, ""));
        Assert.Contains(lockedRejected.Issues, i => i.Message.Contains("locked"));

        await Assert.ThrowsAsync<SpecDecisionRejectedException>(() =>
            specs.ProposeOverlayAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, "{}", ""));
    }

    [Fact]
    public async Task Options_are_ranked_and_filtered_by_decisions_that_already_bind()
    {
        var seed = await SeedAsync();
        using var client = _factory.CreateAuthenticatedHumanClient(seed.Tenant.Id.Value, seed.Owner.Id.Value);
        var root = $"/tenants/{seed.Tenant.Id.Value}/projects/{seed.Project.Id.Value}/spec";

        using var response = await client.GetAsync($"{root}/topics/test.framework.backend/options");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var options = doc!.RootElement.GetProperty("options").EnumerateArray().ToList();

        // Platform language is C#, so xunit and nunit are compatible and junit is not.
        Assert.True(options[0].GetProperty("compatible").GetBoolean());
        Assert.Equal("xunit", options[0].GetProperty("choice").GetProperty("id").GetString());
        var junit = options.Single(o => o.GetProperty("choice").GetProperty("id").GetString() == "junit");
        Assert.False(junit.GetProperty("compatible").GetBoolean());
        Assert.NotEmpty(junit.GetProperty("reasons").EnumerateArray());

        using var catalog = await client.GetAsync($"{root}/catalog");
        using var catalogDoc = await catalog.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Contains(catalogDoc!.RootElement.GetProperty("packs").EnumerateArray(),
            p => p.GetProperty("id").GetString() == "delivery" && !p.GetProperty("enabled").GetBoolean());
        Assert.True(catalogDoc.RootElement.GetProperty("profiles").GetArrayLength() >= 3);

        using var open = await client.GetAsync($"{root}/open-topics");
        Assert.Equal(HttpStatusCode.OK, open.StatusCode);
    }

    [Fact]
    public async Task Agent_discovers_options_open_topics_and_catalog_over_mcp()
    {
        var seed = await SeedAsync();
        Principal agent;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
            agent = Principal.Create(seed.Tenant.Id, $"opt-{Guid.NewGuid():N}@example.test", PrincipalType.AiAgent);
            db.Add(agent);
            db.Add(ProjectMembership.Grant(seed.Tenant.Id, seed.Project.Id, agent.Id, ProjectRole.Member));
            db.Add(AgentCredential.Issue(seed.Tenant.Id, agent.Id, "test", "hashed-test-secret", null));
            foreach (var capability in new[] { AgentCapability.ReadKnowledge, AgentCapability.WriteKnowledge })
            {
                var grant = AgentGrant.Propose(seed.Tenant.Id, seed.Project.Id, agent.Id, capability, null, Classification.Internal);
                grant.Approve(seed.Owner.Id);
                db.Add(grant);
            }

            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateAuthenticatedAgentClient(seed.Tenant.Id.Value, agent.Id.Value);
        var url = $"/tenants/{seed.Tenant.Id.Value}/projects/{seed.Project.Id.Value}/mcp/rpc";

        async Task<JsonDocument> CallAsync(int id, string tool, object arguments)
        {
            using var response = await client.PostAsJsonAsync(
                url, new JsonRpcRequest("2.0", id, McpMethods.CallTool, new { name = tool, arguments }));
            return (await response.Content.ReadFromJsonAsync<JsonDocument>())!;
        }

        using var catalog = await CallAsync(1, "get_spec_catalog", new { });
        Assert.Equal(JsonValueKind.Null, catalog.RootElement.GetProperty("error").ValueKind);
        Assert.True(catalog.RootElement.GetProperty("result").GetProperty("topics").GetArrayLength() >= 10);

        using var options = await CallAsync(2, "list_spec_options", new { topic = "backend.framework" });
        var first = options.RootElement.GetProperty("result").GetProperty("options")[0];
        Assert.Equal("aspnet-core", first.GetProperty("choice").GetProperty("id").GetString());
        Assert.True(first.GetProperty("recommended").GetBoolean());

        using var open = await CallAsync(3, "next_open_topics", new { });
        Assert.Equal(JsonValueKind.Null, open.RootElement.GetProperty("error").ValueKind);

        using var overlay = await CallAsync(4, "propose_spec_overlay",
            new { overlay = JsonDocument.Parse(Overlay).RootElement, rationale = "Release rules" });
        Assert.Equal(JsonValueKind.Null, overlay.RootElement.GetProperty("error").ValueKind);

        // The agent's proposal does not extend the catalog until a person approves it.
        using var after = await CallAsync(5, "list_spec_topics", new { });
        Assert.DoesNotContain(after.RootElement.GetProperty("result").EnumerateArray(),
            t => t.GetProperty("id").GetString() == "acme.release-window");

        using var bad = await CallAsync(6, "propose_spec_overlay",
            new { overlay = JsonDocument.Parse(Overlay.Replace("acme", "backend")).RootElement });
        Assert.NotEqual(JsonValueKind.Null, bad.RootElement.GetProperty("error").ValueKind);
    }

    [Fact]
    public async Task One_approval_makes_every_decision_in_a_profile_bundle_binding_and_edits_are_validated()
    {
        var seed = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var specs = scope.ServiceProvider.GetRequiredService<SpecificationService>();

        // Take the Java profile but edit one line: PostgreSQL 17 instead of 18.
        var catalog = await specs.CatalogAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id);
        var profile = catalog.Profiles.Single(p => p.Id == "java-spring-postgres-react");
        var lines = profile.Decisions
            .Select(d => d.Topic == "db.engine"
                ? d with { Params = new Dictionary<string, string> { ["majorVersion"] = "17" } }
                : d)
            .ToList();

        var bundle = await specs.ProposeBundleAsync(
            seed.Tenant.Id, seed.Project.Id, seed.Member.Id, profile.Id, SpecScopeLevel.Project, lines, "Move to the JVM");

        // Before approval: still the predefined C# stack.
        var before = await specs.ResolveAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, "backend.language");
        Assert.Equal("csharp", before.Binding!.Decision.Choice);

        using (var approval = _factory.Services.CreateScope())
        {
            var db = approval.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
            (await db.KnowledgeItems.SingleAsync(i => i.Id == bundle.Id)).Approve();
            await db.SaveChangesAsync();
        }

        using var fresh = _factory.Services.CreateScope();
        var service = fresh.ServiceProvider.GetRequiredService<SpecificationService>();
        var effective = await service.EffectiveAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id);
        SpecResolution Of(string topic) => effective.Resolutions.Single(r => r.Topic == topic);

        Assert.Equal("java", Of("backend.language").Binding!.Decision.Choice);
        Assert.Equal("spring-boot", Of("backend.framework").Binding!.Decision.Choice);
        Assert.Equal("17", Of("db.engine").Binding!.Decision.Params["majorVersion"]);
        Assert.All(new[] { "backend.language", "db.engine", "test.framework.backend" },
            t => Assert.Equal(bundle.Id.Value, Of(t).Binding!.ItemId));
        Assert.Equal(SpecOrigin.Override, Of("backend.language").Origin);
        Assert.Empty(effective.Incompatibilities);
    }

    [Fact]
    public async Task Bundles_reject_locked_topics_bad_lines_and_duplicates_before_storage()
    {
        var seed = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var specs = scope.ServiceProvider.GetRequiredService<SpecificationService>();
        var none = new Dictionary<string, string>();

        var rejected = await Assert.ThrowsAsync<SpecDecisionRejectedException>(() =>
            specs.ProposeBundleAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, null, SpecScopeLevel.Project,
                [new SpecProfileDecision("source.code-policy", "store-source", none),
                 new SpecProfileDecision("db.engine", "oracle", none),
                 new SpecProfileDecision("api.style", "rest", none),
                 new SpecProfileDecision("api.style", "grpc", none),
                 new SpecProfileDecision("nope.topic", "x", none)], ""));
        Assert.Contains(rejected.Issues, i => i.Message.Contains("locked"));
        Assert.Contains(rejected.Issues, i => i.Path.Contains("db.engine"));
        Assert.Contains(rejected.Issues, i => i.Message.Contains("only once"));
        Assert.Contains(rejected.Issues, i => i.Message.Contains("not defined"));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            specs.ProposeBundleAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, "no-such-profile", SpecScopeLevel.Project, null, ""));
        await Assert.ThrowsAsync<SpecDecisionRejectedException>(() =>
            specs.ProposeBundleAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, null, SpecScopeLevel.Project, null, ""));
    }

    [Fact]
    public async Task Rest_applies_a_profile_bundle_and_reports_no_pending_pack_upgrades()
    {
        var seed = await SeedAsync();
        using var client = _factory.CreateAuthenticatedHumanClient(seed.Tenant.Id.Value, seed.Owner.Id.Value);
        var root = $"/tenants/{seed.Tenant.Id.Value}/projects/{seed.Project.Id.Value}/spec";

        using var created = await client.PostAsJsonAsync($"{root}/bundles",
            new { profileId = "dotnet-postgres-react", level = "project", reason = "Standard stack" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var edited = await client.PostAsJsonAsync($"{root}/bundles", new
        {
            level = "project",
            decisions = new object[] { new { topic = "db.engine", choice = "postgresql", @params = new { majorVersion = 9 } } },
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, edited.StatusCode);

        using var upgrades = await client.GetAsync($"{root}/upgrades");
        Assert.Equal(HttpStatusCode.OK, upgrades.StatusCode);
        using var doc = await upgrades.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(0, doc!.RootElement.GetArrayLength());
    }

    private async Task<Seed> SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
        var tenant = Tenant.Create($"Spec {Guid.NewGuid():N}");
        var owner = Principal.Create(tenant.Id, $"owner-{Guid.NewGuid():N}@example.test", PrincipalType.Human);
        var member = Principal.Create(tenant.Id, $"member-{Guid.NewGuid():N}@example.test", PrincipalType.Human);
        var profile = LifecycleProfile.Create(tenant.Id, Guid.NewGuid().ToString("N"), "Spec", "1");
        var project = Project.Create(tenant.Id, "App", Guid.NewGuid().ToString("N"), null, profile.Id, owner.Id);
        db.AddRange(tenant, owner, member, profile, project);
        db.AddRange(
            ProjectMembership.Grant(tenant.Id, project.Id, owner.Id, ProjectRole.Owner),
            ProjectMembership.Grant(tenant.Id, project.Id, member.Id, ProjectRole.Member));
        await db.SaveChangesAsync();
        return new(tenant, project, owner, member);
    }

    private sealed record Seed(Tenant Tenant, Project Project, Principal Owner, Principal Member);
}
