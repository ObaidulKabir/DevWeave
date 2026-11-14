using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DevWeave.Application.Abstractions;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Application.Planning;
using DevWeave.Contracts.Planned.V1;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;
using DevWeave.Infrastructure.Persistence;
using DevWeave.Mcp.Protocol;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DevWeave.Api.IntegrationTests;

/// <summary>
/// Workthroughs are agent claims about a work package. Only an agent that held a lease on the package may
/// write one, the text goes through the content policy, input is validated (never silently cut), every
/// report is audited, and a final claim stays possible after the plan has stopped the package.
/// </summary>
public sealed class McpWorkthroughIntegrationTests : IClassFixture<IdentityTestFactory>
{
    private readonly IdentityTestFactory _factory;

    public McpWorkthroughIntegrationTests(IdentityTestFactory factory)
    {
        _factory = factory;
        _factory.PrepareDatabase();
    }

    [Fact]
    public async Task Only_a_lease_holder_can_report_and_the_report_is_checked_validated_audited_and_visible()
    {
        var seed = await SeedAsync();
        var owner = new AuthorizedScope(seed.Tenant.Id, seed.Owner.Id, seed.Project.Id, "test", "corr");

        Guid baselineId, campaignId, packageId, otherPackageId, targetId;
        using (var setup = _factory.Services.CreateScope())
        {
            var services = setup.ServiceProvider;
            baselineId = await services.GetRequiredService<IPlanBaselines>().LockAsync(owner,
                new BaselineDraft("Version 1", Array.Empty<ResourceRef>(), Array.Empty<Guid>(), new[] { "criterion" }, 1));
            campaignId = await services.GetRequiredService<ICampaignCoordinator>().CreateAsync(owner,
                new CampaignDraft(baselineId, Array.Empty<Guid>(), "Build version 1", "Mediated", "camp-wt"));
            var scheduler = services.GetRequiredService<IParticipationScheduler>();
            packageId = await scheduler.CreatePackageAsync(owner, new PackageDraft(
                campaignId, baselineId, "First package", [], [], [], [seed.RootRevision.Id.Value]));
            otherPackageId = await scheduler.CreatePackageAsync(owner, new PackageDraft(
                campaignId, baselineId, "Second package", [], [], [], [seed.RootRevision.Id.Value]));
            targetId = await services.GetRequiredService<ICodebaseRegistry>()
                .RegisterAsync(owner, "github", "acme/app", "main", "abc1234");
        }

        using var agentA = _factory.CreateAuthenticatedAgentClient(seed.Tenant.Id.Value, seed.AgentA.Id.Value);
        using var agentB = _factory.CreateAuthenticatedAgentClient(seed.Tenant.Id.Value, seed.AgentB.Id.Value);
        var url = $"/tenants/{seed.Tenant.Id.Value}/projects/{seed.Project.Id.Value}/mcp/rpc";
        var id = 0;

        async Task<JsonElement> Call(HttpClient client, string method, object? @params)
        {
            using var response = await client.PostAsJsonAsync(url, new JsonRpcRequest("2.0", ++id, method, @params));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
            return doc!.RootElement.Clone();
        }

        Task<JsonElement> Tool(HttpClient client, string name, object arguments) =>
            Call(client, McpMethods.CallTool, new { name, arguments });

        static JsonElement Ok(JsonElement reply)
        {
            Assert.Equal(JsonValueKind.Null, reply.GetProperty("error").ValueKind);
            return reply.GetProperty("result");
        }

        static JsonElement Refused(JsonElement reply, int code, string kind)
        {
            var error = reply.GetProperty("error");
            Assert.Equal(code, error.GetProperty("code").GetInt32());
            Assert.Equal(kind, error.GetProperty("data").GetProperty("kind").GetString());
            return error;
        }

        async Task<Guid> RegisterAsync(HttpClient client)
        {
            var runtimeId = Ok(await Tool(client, "register_runtime", new
            {
                runtimeClass = "test-runtime", protocolVersion = McpServer.ProtocolVersion,
                capabilities = new[] { "code.write" }, attestationRef = $"attest-{Guid.NewGuid():N}",
            })).GetProperty("id").GetGuid();
            client.DefaultRequestHeaders.Add("X-Runtime-Id", runtimeId.ToString());
            return runtimeId;
        }

        await RegisterAsync(agentA);
        await RegisterAsync(agentB);

        // Agent A leases the first package; agent B never does.
        Ok(await Tool(agentA, "request_assignment", new
        {
            workPackageId = packageId, resourceScope = "src/app",
            expiresAt = DateTimeOffset.UtcNow.AddMinutes(10).ToString("O"), idempotencyKey = "lease-a",
        }));

        // 1. An agent that never held a lease cannot report on the package.
        Refused(await Tool(agentB, "report_workthrough", new
        {
            workPackageId = packageId, kind = "Succeeded", outcome = "Done", whatSucceeded = "Everything",
        }), -32003, "forbidden");

        // 2. The lease holder can, and the report shows up in the package status.
        var reported = Ok(await Tool(agentA, "report_workthrough", new
        {
            workPackageId = packageId, kind = "Succeeded", outcome = "Retry added",
            whatSucceeded = "Checkout now retries failed payments twice.",
            steps = new[] { "Wrote the retry policy", "Added a test", "  ", "Ran the suite" },
        }));
        Assert.Equal("Succeeded", reported.GetProperty("kind").GetString());
        Assert.Equal(3, reported.GetProperty("steps").GetArrayLength()); // blank step dropped

        var status = Ok(await Tool(agentA, "get_work_status", new { workPackageId = packageId }));
        var latest = status.GetProperty("latestWorkthrough");
        Assert.Equal("Retry added", latest.GetProperty("outcome").GetString());
        Assert.Equal(seed.AgentA.Id.Value, latest.GetProperty("recordedBy").GetGuid());

        // 3. Input is validated: nothing is silently cut off or defaulted.
        Refused(await Tool(agentA, "report_workthrough", new
        { workPackageId = packageId, kind = "Succeeded", outcome = "No detail" }), -32602, "invalid");
        Refused(await Tool(agentA, "report_workthrough", new
        { workPackageId = packageId, kind = "Blocked", outcome = "Stuck" }), -32602, "invalid");
        Refused(await Tool(agentA, "report_workthrough", new
        { workPackageId = packageId, kind = "Maybe", outcome = "?" }), -32602, "invalid");
        Refused(await Tool(agentA, "report_workthrough", new
        {
            workPackageId = packageId, kind = "Succeeded", outcome = "Many", whatSucceeded = "x",
            steps = Enumerable.Range(1, 21).Select(n => $"step {n}").ToArray(),
        }), -32602, "invalid");
        Refused(await Tool(agentA, "report_workthrough", new
        {
            workPackageId = packageId, kind = "Succeeded", outcome = "Long", whatSucceeded = "x",
            steps = new[] { new string('s', 301) },
        }), -32602, "invalid");

        // 4. The content policy applies: no secrets and no source code in the text.
        Refused(await Tool(agentA, "report_workthrough", new
        {
            workPackageId = packageId, kind = "Blocked", outcome = "Cannot connect",
            blockedReason = "Tried with password=hunter2 and it failed.",
        }), -32602, "prohibited-content");
        Refused(await Tool(agentA, "report_workthrough", new
        {
            workPackageId = packageId, kind = "Succeeded", outcome = "Pasted the code",
            whatSucceeded = "using System;\npublic class Checkout { }",
        }), -32602, "prohibited-content");

        // 5. A change set must be a real change set of this same package.
        Refused(await Tool(agentA, "report_workthrough", new
        {
            workPackageId = packageId, kind = "Succeeded", outcome = "Linked", whatSucceeded = "x",
            changeSetId = Guid.NewGuid(),
        }), -32004, "not-found");
        var ownChangeSet = Ok(await Tool(agentA, "submit_change_set", new
        {
            workPackageId = packageId, codebaseTargetId = targetId, providerRef = "github:pr/1", commitSha = "def5678",
        })).GetProperty("changeSetId").GetGuid();
        var otherChangeSet = Ok(await Tool(agentA, "submit_change_set", new
        {
            workPackageId = otherPackageId, codebaseTargetId = targetId, providerRef = "github:pr/2", commitSha = "aaa1111",
        })).GetProperty("changeSetId").GetGuid();
        Refused(await Tool(agentA, "report_workthrough", new
        {
            workPackageId = packageId, kind = "Succeeded", outcome = "Wrong link", whatSucceeded = "x",
            changeSetId = otherChangeSet,
        }), -32004, "not-found");
        Ok(await Tool(agentA, "report_workthrough", new
        {
            workPackageId = packageId, kind = "Succeeded", outcome = "Linked", whatSucceeded = "Merged the retry work.",
            changeSetId = ownChangeSet,
        }));

        // 6. Every accepted report left an audit record.
        using (var audit = _factory.Services.CreateScope())
        {
            var records = await audit.ServiceProvider.GetRequiredService<IAuditRepository>()
                .ListByTenantAsync(seed.Tenant.Id);
            Assert.Equal(2, records.Count(r => r.Action == "work.package.workthrough_recorded"));
        }

        // 7. The Owner changes the plan and locks version 2: the package is stopped...
        using (var change = _factory.Services.CreateScope())
        {
            var db = change.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
            var root = await db.KnowledgeItems.SingleAsync(i => i.Id == seed.Root.Id);
            var next = root.AddRevision("1.0", "{\"summary\":\"Changed idea\"}", seed.Owner.Id, "changed", null, null, root.CurrentRevisionNumber);
            db.Add(next);
            await db.SaveChangesAsync();
            root.Approve(next.Id);
            await db.SaveChangesAsync();
            await change.ServiceProvider.GetRequiredService<IPlanBaselines>().LockAsync(owner,
                new BaselineDraft("Version 2", Array.Empty<ResourceRef>(), Array.Empty<Guid>(), new[] { "criterion" }, 2));
        }

        Refused(await Tool(agentA, "submit_change_set", new
        {
            workPackageId = packageId, codebaseTargetId = targetId, providerRef = "github:pr/3", commitSha = "bbb2222",
        }), -32009, "conflict");

        // ...but the agent that did the work can still make a final, honest claim, and the latest one is shown.
        Ok(await Tool(agentA, "report_workthrough", new
        {
            workPackageId = packageId, kind = "Blocked", outcome = "Stopped by a plan change",
            blockedReason = "Plan version 2 was locked while the retry tests were still running.",
            whatSucceeded = "The retry policy itself was finished.",
        }));
        var finalStatus = Ok(await Tool(agentA, "get_work_status", new { workPackageId = packageId }));
        Assert.False(finalStatus.GetProperty("canProgress").GetBoolean());
        Assert.Equal("Blocked", finalStatus.GetProperty("latestWorkthrough").GetProperty("kind").GetString());

        // Agent B is still not allowed, plan change or not.
        Refused(await Tool(agentB, "report_workthrough", new
        {
            workPackageId = packageId, kind = "Succeeded", outcome = "Hijack", whatSucceeded = "x",
        }), -32003, "forbidden");

        // 8. The REST list Studio reads from carries the same latest report.
        using var human = _factory.CreateAuthenticatedHumanClient(seed.Tenant.Id.Value, seed.Owner.Id.Value);
        using var listed = await human.GetAsync(
            $"/tenants/{seed.Tenant.Id.Value}/projects/{seed.Project.Id.Value}/coordination/campaigns/{campaignId}/packages");
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        using var listedDoc = await listed.Content.ReadFromJsonAsync<JsonDocument>();
        var withReport = listedDoc!.RootElement.EnumerateArray()
            .Single(p => p.GetProperty("id").GetGuid() == packageId);
        Assert.Equal("Blocked", withReport.GetProperty("latestWorkthrough").GetProperty("kind").GetString());
        var withoutReport = listedDoc.RootElement.EnumerateArray()
            .Single(p => p.GetProperty("id").GetGuid() == otherPackageId);
        Assert.Equal(JsonValueKind.Null, withoutReport.GetProperty("latestWorkthrough").ValueKind);
    }

    [Fact]
    public async Task Tool_arguments_must_be_a_json_object_and_other_shapes_are_refused_clearly()
    {
        var seed = await SeedAsync();
        using var agent = _factory.CreateAuthenticatedAgentClient(seed.Tenant.Id.Value, seed.AgentA.Id.Value);
        var url = $"/tenants/{seed.Tenant.Id.Value}/projects/{seed.Project.Id.Value}/mcp/rpc";

        async Task<JsonElement> Call(object arguments)
        {
            using var response = await agent.PostAsJsonAsync(url, new JsonRpcRequest(
                "2.0", 1, McpMethods.CallTool, new { name = "get_plan_status", arguments }));
            using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
            return doc!.RootElement.Clone();
        }

        // An empty object is fine.
        Assert.Equal(JsonValueKind.Null, (await Call(new { })).GetProperty("error").ValueKind);

        // A one-element array (what some PowerShell clients send) and a key/value list are refused.
        foreach (var bad in new object[] { new object[] { new { } }, new object[] { new { Key = "a", Value = "b" } }, "text", 5 })
        {
            var error = (await Call(bad)).GetProperty("error");
            Assert.Equal(-32602, error.GetProperty("code").GetInt32());
            Assert.Contains("JSON object", error.GetProperty("message").GetString());
        }
    }

    private async Task<Seed> SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
        var tenant = Tenant.Create($"Wt {Guid.NewGuid():N}");
        var owner = Principal.Create(tenant.Id, $"owner-{Guid.NewGuid():N}@example.test", PrincipalType.Human);
        var agentA = Principal.Create(tenant.Id, $"a-{Guid.NewGuid():N}@example.test", PrincipalType.AiAgent);
        var agentB = Principal.Create(tenant.Id, $"b-{Guid.NewGuid():N}@example.test", PrincipalType.AiAgent);
        var profile = LifecycleProfile.Create(tenant.Id, Guid.NewGuid().ToString("N"), "Plan", "1");
        var project = Project.Create(tenant.Id, "App", Guid.NewGuid().ToString("N"), null, profile.Id, owner.Id);
        db.AddRange(tenant, owner, agentA, agentB, profile, project);
        db.Add(ProjectMembership.Grant(tenant.Id, project.Id, owner.Id, ProjectRole.Owner));
        foreach (var agent in new[] { agentA, agentB })
        {
            db.Add(ProjectMembership.Grant(tenant.Id, project.Id, agent.Id, ProjectRole.Member));
            db.Add(AgentCredential.Issue(tenant.Id, agent.Id, "test", "hashed-test-secret", null));
            foreach (var capability in new[] { AgentCapability.ReadKnowledge, AgentCapability.WriteKnowledge })
            {
                var grant = AgentGrant.Propose(tenant.Id, project.Id, agent.Id, capability, null, Classification.Internal);
                grant.Approve(owner.Id);
                db.Add(grant);
            }
        }

        var root = KnowledgeItem.Create(tenant.Id, project.Id, $"idea-{Guid.NewGuid():N}", "idea", Classification.Internal,
            RepresentationKind.Text, "doc.note", "1.0", owner.Id);
        var rootRevision = root.AddRevision("1.0", "{\"summary\":\"Plan\"}", owner.Id, "Initial idea", null, null, 0);
        db.AddRange(root, rootRevision);
        await db.SaveChangesAsync();
        root.Approve(rootRevision.Id);
        await db.SaveChangesAsync();

        await scope.ServiceProvider.GetRequiredService<PlanDesignService>()
            .AttachAsync(tenant.Id, project.Id, owner.Id, root.Id, null, 0);
        return new Seed(tenant, project, owner, agentA, agentB, root, rootRevision);
    }

    private sealed record Seed(Tenant Tenant, Project Project, Principal Owner, Principal AgentA, Principal AgentB,
        KnowledgeItem Root, KnowledgeRevision RootRevision);
}
