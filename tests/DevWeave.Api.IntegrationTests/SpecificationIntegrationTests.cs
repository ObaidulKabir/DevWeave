using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DevWeave.Application.Abstractions;
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
