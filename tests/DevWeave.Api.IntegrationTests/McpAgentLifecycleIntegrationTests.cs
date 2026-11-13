using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
/// Drives a complete agent session over MCP JSON-RPC, as described in docs/agent-guide.md: discover the
/// plan, create scoped work, lease it, submit a change set, queue integration, and then see a newer plan
/// version stop the work with stable, machine-readable refusals.
/// </summary>
public sealed class McpAgentLifecycleIntegrationTests : IClassFixture<IdentityTestFactory>
{
    private readonly IdentityTestFactory _factory;

    public McpAgentLifecycleIntegrationTests(IdentityTestFactory factory)
    {
        _factory = factory;
        _factory.PrepareDatabase();
    }

    [Fact]
    public async Task Agent_runs_the_full_execution_loop_over_mcp_and_is_stopped_cleanly_by_a_plan_change()
    {
        var seed = await SeedAsync();
        var owner = new AuthorizedScope(seed.Tenant.Id, seed.Owner.Id, seed.Project.Id, "test", "corr");

        Guid baselineId, campaignId, targetId;
        using (var setup = _factory.Services.CreateScope())
        {
            var services = setup.ServiceProvider;
            baselineId = await services.GetRequiredService<IPlanBaselines>().LockAsync(owner,
                new BaselineDraft("Version 1", Array.Empty<ResourceRef>(), Array.Empty<Guid>(), new[] { "criterion" }, 1));
            campaignId = await services.GetRequiredService<ICampaignCoordinator>().CreateAsync(owner,
                new CampaignDraft(baselineId, Array.Empty<Guid>(), "Build version 1", "Mediated", "camp-mcp"));
            targetId = await services.GetRequiredService<ICodebaseRegistry>()
                .RegisterAsync(owner, "github", "acme/app", "main", "abc1234");
        }

        using var agent = _factory.CreateAuthenticatedAgentClient(seed.Tenant.Id.Value, seed.Agent.Id.Value);
        var url = $"/tenants/{seed.Tenant.Id.Value}/projects/{seed.Project.Id.Value}/mcp/rpc";
        var id = 0;

        async Task<JsonElement> Call(string method, object? @params)
        {
            using var response = await agent.PostAsJsonAsync(url, new JsonRpcRequest("2.0", ++id, method, @params));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
            return doc!.RootElement.Clone();
        }

        Task<JsonElement> Tool(string name, object arguments) =>
            Call(McpMethods.CallTool, new { name, arguments });

        static JsonElement Ok(JsonElement reply)
        {
            Assert.Equal(JsonValueKind.Null, reply.GetProperty("error").ValueKind);
            return reply.GetProperty("result");
        }

        static void Refused(JsonElement reply, int code, string kind)
        {
            var error = reply.GetProperty("error");
            Assert.Equal(code, error.GetProperty("code").GetInt32());
            Assert.Equal(kind, error.GetProperty("data").GetProperty("kind").GetString());
        }

        // 1. Discover: every execution tool the guide documents is advertised.
        var tools = Ok(await Call(McpMethods.ListTools, new { })).GetProperty("tools")
            .EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToHashSet();
        foreach (var expected in new[]
                 {
                     "get_plan_status", "list_work_packages", "get_work_status", "create_work_package",
                     "request_assignment", "heartbeat_lease", "release_lease", "submit_change_set",
                     "request_integration", "read_plan_outline", "resolve_spec", "get_effective_spec",
                 })
            Assert.Contains(expected, tools);

        // 2. Register this runtime and send its id on every later call.
        var runtimeId = Ok(await Tool("register_runtime", new
        {
            runtimeClass = "test-runtime", protocolVersion = McpServer.ProtocolVersion,
            capabilities = new[] { "code.write" }, attestationRef = "attest-1",
        })).GetProperty("id").GetGuid();
        agent.DefaultRequestHeaders.Add("X-Runtime-Id", runtimeId.ToString());

        // 3. Read the plan: version 1 is current and the campaign runs on it.
        var plan = Ok(await Tool("get_plan_status", new { }));
        Assert.Equal(1, plan.GetProperty("currentPlanVersion").GetInt32());
        var campaign = Assert.Single(plan.GetProperty("campaigns").EnumerateArray());
        Assert.True(campaign.GetProperty("onCurrentPlan").GetBoolean());

        // 4. Create scoped work: it implements the approved root revision of this version.
        Refused(await Tool("create_work_package", new
        {
            campaignId, baselineId, objective = "Outside the plan", implementsRevisionIds = new[] { Guid.NewGuid() },
        }), -32009, "conflict");
        var packageId = Ok(await Tool("create_work_package", new
        {
            campaignId, baselineId, objective = "Implement the idea",
            implementsRevisionIds = new[] { seed.RootRevision.Id.Value },
            specTopics = new[] { "backend.language" },
        })).GetProperty("id").GetGuid();

        var listed = Assert.Single(Ok(await Tool("list_work_packages", new { campaignId })).EnumerateArray());
        Assert.True(listed.GetProperty("canProgress").GetBoolean());
        Assert.False(listed.GetProperty("unscoped").GetBoolean());

        // 5. Lease, heartbeat, submit and queue.
        var lease = Ok(await Tool("request_assignment", new
        {
            workPackageId = packageId, resourceScope = "src/app",
            expiresAt = DateTimeOffset.UtcNow.AddMinutes(10).ToString("O"), idempotencyKey = "lease-1",
        }));
        var leaseId = lease.GetProperty("id").GetGuid();
        var fence = lease.GetProperty("fence").GetInt64();
        Ok(await Tool("heartbeat_lease", new
        {
            leaseId, expectedFence = fence, expiresAt = DateTimeOffset.UtcNow.AddMinutes(15).ToString("O"),
        }));
        var changeSetId = Ok(await Tool("submit_change_set", new
        {
            workPackageId = packageId, codebaseTargetId = targetId, providerRef = "github:pr/1",
            commitSha = "def5678", changedPaths = new[] { "src/app/Program.cs" },
        })).GetProperty("changeSetId").GetGuid();
        var queueItemId = Ok(await Tool("request_integration", new
        {
            changeSetId, idempotencyKey = $"queue-{Guid.NewGuid():N}",
        })).GetProperty("queueItemId").GetGuid();

        // 6. The Owner changes the plan and locks version 2.
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

        // 7. The agent sees why it must stop, and every further step is refused with a stable code.
        Assert.Equal(2, Ok(await Tool("get_plan_status", new { })).GetProperty("currentPlanVersion").GetInt32());
        var status = Ok(await Tool("get_work_status", new { workPackageId = packageId }));
        Assert.False(status.GetProperty("canProgress").GetBoolean());
        Assert.False(status.GetProperty("onCurrentPlan").GetBoolean());
        Assert.Contains("suspended", status.GetProperty("blockedReason").GetString());

        Refused(await Tool("heartbeat_lease", new
        {
            leaseId, expectedFence = fence, expiresAt = DateTimeOffset.UtcNow.AddMinutes(20).ToString("O"),
        }), -32009, "conflict");
        Refused(await Tool("submit_change_set", new
        {
            workPackageId = packageId, codebaseTargetId = targetId, providerRef = "github:pr/2", commitSha = "aaa1111",
        }), -32009, "conflict");
        Refused(await Tool("get_work_status", new { workPackageId = Guid.NewGuid() }), -32004, "not-found");
        Refused(await Tool("no_such_tool", new { }), -32601, "unknown-tool");

        // Releasing is always allowed so the agent can let go cleanly.
        Assert.True(Ok(await Tool("release_lease", new { leaseId, expectedFence = fence }))
            .GetProperty("released").GetBoolean());

        // Work queued before the change is rejected at decision time, never merged.
        using var decide = _factory.Services.CreateScope();
        var decision = await decide.ServiceProvider.GetRequiredService<IIntegrationQueue>().DecideAsync(owner, queueItemId, fence);
        Assert.Equal("Rejected", decision.Outcome);
    }

    private async Task<Seed> SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
        var tenant = Tenant.Create($"Mcp {Guid.NewGuid():N}");
        var owner = Principal.Create(tenant.Id, $"owner-{Guid.NewGuid():N}@example.test", PrincipalType.Human);
        var agent = Principal.Create(tenant.Id, $"agent-{Guid.NewGuid():N}@example.test", PrincipalType.AiAgent);
        var profile = LifecycleProfile.Create(tenant.Id, Guid.NewGuid().ToString("N"), "Plan", "1");
        var project = Project.Create(tenant.Id, "App", Guid.NewGuid().ToString("N"), null, profile.Id, owner.Id);
        db.AddRange(tenant, owner, agent, profile, project);
        db.AddRange(
            ProjectMembership.Grant(tenant.Id, project.Id, owner.Id, ProjectRole.Owner),
            ProjectMembership.Grant(tenant.Id, project.Id, agent.Id, ProjectRole.Member));
        db.Add(AgentCredential.Issue(tenant.Id, agent.Id, "test", "hashed-test-secret", null));
        foreach (var capability in new[] { AgentCapability.ReadKnowledge, AgentCapability.WriteKnowledge })
        {
            var grant = AgentGrant.Propose(tenant.Id, project.Id, agent.Id, capability, null, Classification.Internal);
            grant.Approve(owner.Id);
            db.Add(grant);
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
        return new Seed(tenant, project, owner, agent, root, rootRevision);
    }

    private sealed record Seed(Tenant Tenant, Project Project, Principal Owner, Principal Agent,
        KnowledgeItem Root, KnowledgeRevision RootRevision);
}
