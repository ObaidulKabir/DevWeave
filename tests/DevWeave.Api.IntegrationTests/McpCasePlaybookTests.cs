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
/// Case playbook gaps from docs/agent-guide.md. Each part is one fact and is run before the next part
/// is added. Already covered elsewhere: C2 specifications, C3 plan import, C4 expansion and Work claims,
/// C8 reported Completed staying unverified.
/// </summary>
public sealed class McpCasePlaybookTests : IClassFixture<IdentityTestFactory>
{
    private readonly IdentityTestFactory _factory;

    public McpCasePlaybookTests(IdentityTestFactory factory)
    {
        _factory = factory;
        _factory.PrepareDatabase();
    }

    [Fact]
    public async Task Part1_handshake_lists_cases_and_hides_human_gates()
    {
        var seed = await SeedAsync();
        using var executor = _factory.CreateAuthenticatedAgentClient(seed.Tenant.Id.Value, seed.Executor.Id.Value);
        using var planner = _factory.CreateAuthenticatedAgentClient(seed.Tenant.Id.Value, seed.Planner.Id.Value);
        var url = $"/tenants/{seed.Tenant.Id.Value}/projects/{seed.Project.Id.Value}/mcp/rpc";

        var init = await CallAsync(executor, url, 1, McpMethods.Initialize, null);
        var instructions = McpJson.Result(init).GetProperty("instructions").GetString();
        Assert.NotNull(instructions);
        Assert.Contains("Pick one case", instructions, StringComparison.Ordinal);
        Assert.Contains("C0 connect", instructions, StringComparison.Ordinal);
        Assert.Contains("C3 empty plan", instructions, StringComparison.Ordinal);
        Assert.Contains("C5 plan moved", instructions, StringComparison.Ordinal);
        Assert.Contains("C6 code", instructions, StringComparison.Ordinal);
        Assert.Contains("C9 handoff", instructions, StringComparison.Ordinal);
        Assert.Contains("C11 production", instructions, StringComparison.Ordinal);
        Assert.Contains("there is no deploy tool", instructions, StringComparison.Ordinal);
        Assert.Contains("you cannot verify or approve", instructions, StringComparison.Ordinal);

        var executionTools = await ToolNamesAsync(executor, url, 2);
        Assert.Contains("request_assignment", executionTools);
        Assert.Contains("report_workthrough", executionTools);
        Assert.Contains("release_lease", executionTools);
        Assert.DoesNotContain("propose_initial_plan", executionTools);
        Assert.DoesNotContain("verify_work_item", executionTools);
        Assert.DoesNotContain("approve_work_item", executionTools);
        Assert.DoesNotContain(executionTools, name => name.Contains("deploy", StringComparison.OrdinalIgnoreCase));

        var planningTools = await ToolNamesAsync(planner, url, 3);
        Assert.Contains("propose_initial_plan", planningTools);
        Assert.Contains("report_work_item", planningTools);
        Assert.DoesNotContain("request_assignment", planningTools);
        Assert.DoesNotContain("submit_change_set", planningTools);
        Assert.DoesNotContain(planningTools, name => name.Contains("deploy", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Part2_two_devices_keep_disjoint_leases_and_hand_off()
    {
        var seed = await SeedAsync();
        var owner = new AuthorizedScope(seed.Tenant.Id, seed.Owner.Id, seed.Project.Id, "lock", "part2");
        Guid baselineId, campaignId, targetId;
        using (var setup = _factory.Services.CreateScope())
        {
            var services = setup.ServiceProvider;
            baselineId = await services.GetRequiredService<IPlanBaselines>().LockAsync(owner,
                new BaselineDraft("Version 1", [], [], ["criterion"], 1));
            campaignId = await services.GetRequiredService<ICampaignCoordinator>().CreateAsync(owner,
                new CampaignDraft(baselineId, [], "Build", "Mediated", "camp-part2"));
            targetId = await services.GetRequiredService<ICodebaseRegistry>()
                .RegisterAsync(owner, "github", "acme/app", "main", "abc1234");
        }

        using var deviceA = _factory.CreateAuthenticatedAgentClient(seed.Tenant.Id.Value, seed.Executor.Id.Value);
        using var deviceB = _factory.CreateAuthenticatedAgentClient(seed.Tenant.Id.Value, seed.ExecutorB.Id.Value);
        var url = $"/tenants/{seed.Tenant.Id.Value}/projects/{seed.Project.Id.Value}/mcp/rpc";
        var id = 0;

        async Task<JsonElement> Tool(HttpClient client, string name, object arguments) =>
            await CallAsync(client, url, ++id, McpMethods.CallTool, new { name, arguments });

        static void Refused(JsonElement reply, int code, string kind)
        {
            var error = reply.GetProperty("error");
            Assert.Equal(code, error.GetProperty("code").GetInt32());
            Assert.Equal(kind, error.GetProperty("data").GetProperty("kind").GetString());
        }

        var targets = McpJson.Payload(await Tool(deviceA, "list_codebase_targets", new { }))
            .GetProperty("codebaseTargets");
        Assert.Contains(targets.EnumerateArray(), target =>
            target.GetProperty("id").GetGuid() == targetId &&
            target.GetProperty("repositoryId").GetString() == "acme/app");

        var runtimeA = McpJson.Payload(await Tool(deviceA, "register_runtime", new
        {
            runtimeClass = "laptop-a", protocolVersion = McpServer.ProtocolVersion,
            capabilities = new[] { "code.write" }, attestationRef = "attest-a",
        })).GetProperty("id").GetGuid();
        deviceA.DefaultRequestHeaders.Add("X-Runtime-Id", runtimeA.ToString());
        var runtimeB = McpJson.Payload(await Tool(deviceB, "register_runtime", new
        {
            runtimeClass = "laptop-b", protocolVersion = McpServer.ProtocolVersion,
            capabilities = new[] { "code.write" }, attestationRef = "attest-b",
        })).GetProperty("id").GetGuid();
        deviceB.DefaultRequestHeaders.Add("X-Runtime-Id", runtimeB.ToString());

        var packageId = McpJson.Payload(await Tool(deviceA, "create_work_package", new
        {
            campaignId, baselineId, objective = "Shared codebase",
            implementsRevisionIds = new[] { seed.RootRevision.Id.Value },
        })).GetProperty("id").GetGuid();

        var leaseA = McpJson.Payload(await Tool(deviceA, "request_assignment", new
        {
            workPackageId = packageId, resourceScope = "paths:/payments",
            expiresAt = DateTimeOffset.UtcNow.AddMinutes(20).ToString("O"), idempotencyKey = "lease-a",
        }));
        var leaseAId = leaseA.GetProperty("id").GetGuid();
        var fenceA = leaseA.GetProperty("fence").GetInt64();

        Refused(await Tool(deviceB, "request_assignment", new
        {
            workPackageId = packageId, resourceScope = "paths:/payments",
            expiresAt = DateTimeOffset.UtcNow.AddMinutes(20).ToString("O"), idempotencyKey = "lease-conflict",
        }), -32009, "conflict");

        var leaseB = McpJson.Payload(await Tool(deviceB, "request_assignment", new
        {
            workPackageId = packageId, resourceScope = "paths:/ui",
            expiresAt = DateTimeOffset.UtcNow.AddMinutes(20).ToString("O"), idempotencyKey = "lease-b",
        }));
        Assert.NotEqual(leaseAId, leaseB.GetProperty("id").GetGuid());

        Refused(await Tool(deviceB, "heartbeat_lease", new
        {
            leaseId = leaseAId, expectedFence = fenceA,
            expiresAt = DateTimeOffset.UtcNow.AddMinutes(30).ToString("O"),
        }), -32003, "forbidden");
        Refused(await Tool(deviceB, "release_lease", new { leaseId = leaseAId, expectedFence = fenceA }),
            -32003, "forbidden");

        var channelId = McpJson.Payload(await Tool(deviceA, "open_channel", new
        {
            channelType = "Campaign", campaignId,
        })).GetProperty("id").GetGuid();
        McpJson.Payload(await Tool(deviceA, "send_project_message", new
        {
            channelId, messageType = "Handoff",
            payload = new { sha = "def5678", paths = new[] { "src/Payments" }, remaining = "finish refunds" },
            idempotencyKey = "handoff-1",
        }));
        var replay = McpJson.Payload(await Tool(deviceB, "read_messages_after_cursor", new
        {
            channelId, afterSequence = 0, limit = 10,
        })).GetProperty("messages").ToString();
        Assert.Contains("def5678", replay, StringComparison.Ordinal);
        Assert.Contains("finish refunds", replay, StringComparison.Ordinal);

        Assert.True(McpJson.Payload(await Tool(deviceA, "release_lease", new
        {
            leaseId = leaseAId, expectedFence = fenceA,
        })).GetProperty("released").GetBoolean());

        var taken = McpJson.Payload(await Tool(deviceB, "request_assignment", new
        {
            workPackageId = packageId, resourceScope = "paths:/payments",
            expiresAt = DateTimeOffset.UtcNow.AddMinutes(20).ToString("O"), idempotencyKey = "lease-handoff",
        }));
        Assert.NotEqual(leaseAId, taken.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Part3_plan_change_allows_blocked_report_and_refuses_further_work()
    {
        var seed = await SeedAsync();
        var owner = new AuthorizedScope(seed.Tenant.Id, seed.Owner.Id, seed.Project.Id, "lock", "part3");
        Guid baselineId, campaignId, packageId, leaseId, runtimeId;
        long fence;
        using (var setup = _factory.Services.CreateScope())
        {
            var services = setup.ServiceProvider;
            baselineId = await services.GetRequiredService<IPlanBaselines>().LockAsync(owner,
                new BaselineDraft("Version 1", [], [], ["criterion"], 1));
            campaignId = await services.GetRequiredService<ICampaignCoordinator>().CreateAsync(owner,
                new CampaignDraft(baselineId, [], "Build", "Mediated", "camp-part3"));
            runtimeId = await services.GetRequiredService<IRuntimeRegistry>().RegisterAsync(owner,
                new RuntimeRegistration(seed.Executor.Id.Value, "laptop", "1.0", ["code.write"], "attest"));
            packageId = await services.GetRequiredService<IParticipationScheduler>().CreatePackageAsync(owner,
                new PackageDraft(campaignId, baselineId, "Implement", [], [], [], [seed.RootRevision.Id.Value]));
            var lease = await services.GetRequiredService<ILeaseAuthority>().AcquireAsync(owner,
                new LeaseRequest(packageId, runtimeId, "paths:/api", DateTimeOffset.UtcNow.AddMinutes(20), 0, "lease-part3"));
            leaseId = lease.Id;
            fence = lease.Fence;
        }

        using var agent = _factory.CreateAuthenticatedAgentClient(seed.Tenant.Id.Value, seed.Executor.Id.Value);
        agent.DefaultRequestHeaders.Add("X-Runtime-Id", runtimeId.ToString());
        var url = $"/tenants/{seed.Tenant.Id.Value}/projects/{seed.Project.Id.Value}/mcp/rpc";
        var id = 0;
        Task<JsonElement> Tool(string name, object arguments) =>
            CallAsync(agent, url, ++id, McpMethods.CallTool, new { name, arguments });

        using (var change = _factory.Services.CreateScope())
        {
            var db = change.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
            var root = await db.KnowledgeItems.SingleAsync(item => item.Id == seed.RootRevision.KnowledgeItemId);
            var next = root.AddRevision("1.0", "{\"summary\":\"Changed\"}", seed.Owner.Id, "changed", null, null, root.CurrentRevisionNumber);
            db.Add(next);
            await db.SaveChangesAsync();
            root.Approve(next.Id);
            await db.SaveChangesAsync();
            await change.ServiceProvider.GetRequiredService<IPlanBaselines>().LockAsync(owner,
                new BaselineDraft("Version 2", [], [], ["criterion"], 2));
        }

        var status = McpJson.Payload(await Tool("get_work_status", new { workPackageId = packageId }));
        Assert.False(status.GetProperty("canProgress").GetBoolean());
        Assert.False(status.GetProperty("onCurrentPlan").GetBoolean());

        var blocked = McpJson.Payload(await Tool("report_workthrough", new
        {
            workPackageId = packageId, kind = "Blocked", outcome = "Stopped for the new baseline",
            blockedReason = "Campaign suspended",
        }));
        Assert.Equal("Blocked", blocked.GetProperty("kind").GetString());

        var succeeded = await Tool("report_workthrough", new
        {
            workPackageId = packageId, kind = "Succeeded", outcome = "Claimed done after the plan moved",
            whatSucceeded = "Nothing",
        });
        Assert.Equal(-32009, succeeded.GetProperty("error").GetProperty("code").GetInt32());

        var renewed = await Tool("heartbeat_lease", new
        {
            leaseId, expectedFence = fence, expiresAt = DateTimeOffset.UtcNow.AddMinutes(30).ToString("O"),
        });
        Assert.Equal(-32009, renewed.GetProperty("error").GetProperty("code").GetInt32());

        var released = McpJson.Payload(await Tool("release_lease", new { leaseId, expectedFence = fence }));
        Assert.True(released.GetProperty("released").GetBoolean());

        var again = await Tool("request_assignment", new
        {
            workPackageId = packageId, resourceScope = "paths:/api",
            expiresAt = DateTimeOffset.UtcNow.AddMinutes(10).ToString("O"), idempotencyKey = "lease-after",
        });
        Assert.Equal(-32009, again.GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task Part4_channel_message_does_not_become_a_plan()
    {
        var seed = await SeedAsync();
        var owner = new AuthorizedScope(seed.Tenant.Id, seed.Owner.Id, seed.Project.Id, "lock", "part4");
        Guid campaignId;
        using (var setup = _factory.Services.CreateScope())
        {
            var services = setup.ServiceProvider;
            var baselineId = await services.GetRequiredService<IPlanBaselines>().LockAsync(owner,
                new BaselineDraft("Version 1", [], [], ["criterion"], 1));
            campaignId = await services.GetRequiredService<ICampaignCoordinator>().CreateAsync(owner,
                new CampaignDraft(baselineId, [], "Build", "Mediated", "camp-part4"));
        }

        using var agent = _factory.CreateAuthenticatedAgentClient(seed.Tenant.Id.Value, seed.Executor.Id.Value);
        var runtimeId = McpJson.Payload(await CallAsync(agent,
            $"/tenants/{seed.Tenant.Id.Value}/projects/{seed.Project.Id.Value}/mcp/rpc",
            1, McpMethods.CallTool, new
            {
                name = "register_runtime",
                arguments = new
                {
                    runtimeClass = "laptop", protocolVersion = McpServer.ProtocolVersion,
                    capabilities = new[] { "code.write" }, attestationRef = "attest-part4",
                },
            })).GetProperty("id").GetGuid();
        agent.DefaultRequestHeaders.Add("X-Runtime-Id", runtimeId.ToString());
        var url = $"/tenants/{seed.Tenant.Id.Value}/projects/{seed.Project.Id.Value}/mcp/rpc";

        var channelId = McpJson.Payload(await CallAsync(agent, url, 2, McpMethods.CallTool, new
        {
            name = "open_channel",
            arguments = new { channelType = "Campaign", campaignId },
        })).GetProperty("id").GetGuid();

        int knowledgeBefore;
        using (var before = _factory.Services.CreateScope())
        {
            knowledgeBefore = await before.ServiceProvider.GetRequiredService<DevWeaveDbContext>()
                .KnowledgeItems.CountAsync(item => item.ProjectId == seed.Project.Id);
        }

        McpJson.Payload(await CallAsync(agent, url, 3, McpMethods.CallTool, new
        {
            name = "send_project_message",
            arguments = new
            {
                channelId,
                messageType = "Brainstorm",
                payload = new { title = "Secret billing idea", knowledge = new[] { new { key = "billing" } } },
                idempotencyKey = "brainstorm-1",
            },
        }));

        using (var after = _factory.Services.CreateScope())
        {
            var db = after.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
            var knowledgeAfter = await db.KnowledgeItems.CountAsync(item => item.ProjectId == seed.Project.Id);
            Assert.Equal(knowledgeBefore, knowledgeAfter);
            Assert.DoesNotContain(await db.KnowledgeItems.Where(item => item.ProjectId == seed.Project.Id).ToListAsync(),
                item => item.Key == "billing" || item.Title == "Secret billing idea");
        }

        using var planner = _factory.CreateAuthenticatedAgentClient(seed.Tenant.Id.Value, seed.Planner.Id.Value);
        var outline = McpJson.Payload(await CallAsync(planner, url, 4, McpMethods.CallTool, new
        {
            name = "read_plan_outline",
            arguments = new { },
        })).ToString();
        Assert.DoesNotContain("Secret billing idea", outline, StringComparison.Ordinal);
        Assert.DoesNotContain("billing", outline, StringComparison.Ordinal);
    }

    private static async Task<JsonElement> CallAsync(
        HttpClient client, string url, int id, string method, object? @params)
    {
        using var response = await client.PostAsJsonAsync(url, new JsonRpcRequest("2.0", id, method, @params));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return doc!.RootElement.Clone();
    }

    private static async Task<HashSet<string>> ToolNamesAsync(HttpClient client, string url, int id)
    {
        var listed = await CallAsync(client, url, id, McpMethods.ListTools, new { });
        return McpJson.Payload(listed).GetProperty("tools").EnumerateArray()
            .Select(tool => tool.GetProperty("name").GetString()!)
            .ToHashSet();
    }

    private async Task<Seed> SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
        var tenant = Tenant.Create($"Cases {Guid.NewGuid():N}");
        var owner = Principal.Create(tenant.Id, $"owner-{Guid.NewGuid():N}@example.test", PrincipalType.Human);
        var planner = Principal.Create(tenant.Id, $"planner-{Guid.NewGuid():N}@example.test", PrincipalType.AiAgent);
        var executor = Principal.Create(tenant.Id, $"executor-{Guid.NewGuid():N}@example.test", PrincipalType.AiAgent);
        var executorB = Principal.Create(tenant.Id, $"executor-b-{Guid.NewGuid():N}@example.test", PrincipalType.AiAgent);
        var profile = LifecycleProfile.Create(tenant.Id, Guid.NewGuid().ToString("N"), "Plan", "1");
        var project = Project.Create(tenant.Id, "App", Guid.NewGuid().ToString("N"), null, profile.Id, owner.Id);
        db.AddRange(tenant, owner, planner, executor, executorB, profile, project);
        db.AddRange(
            ProjectMembership.Grant(tenant.Id, project.Id, owner.Id, ProjectRole.Owner),
            ProjectMembership.Grant(tenant.Id, project.Id, planner.Id, ProjectRole.Member),
            ProjectMembership.Grant(tenant.Id, project.Id, executor.Id, ProjectRole.Member),
            ProjectMembership.Grant(tenant.Id, project.Id, executorB.Id, ProjectRole.Member));
        db.Add(AgentCredential.Issue(tenant.Id, planner.Id, "test", "hashed-planner", null));
        db.Add(AgentCredential.Issue(tenant.Id, executor.Id, "test", "hashed-executor", null));
        db.Add(AgentCredential.Issue(tenant.Id, executorB.Id, "test", "hashed-executor-b", null));

        foreach (var capability in new[] { AgentCapability.ReadKnowledge, AgentCapability.WriteKnowledge })
            db.Add(Approved(tenant.Id, project.Id, planner.Id, owner.Id, capability));
        foreach (var agentId in new[] { executor.Id, executorB.Id })
        {
            db.Add(Approved(tenant.Id, project.Id, agentId, owner.Id, AgentCapability.ExecuteWorkflow));
            db.Add(Approved(tenant.Id, project.Id, agentId, owner.Id, AgentCapability.ReadKnowledge));
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

        return new Seed(tenant, project, owner, planner, executor, executorB, rootRevision);
    }

    private static AgentGrant Approved(
        TenantId tenantId, ProjectId projectId, PrincipalId agentId, PrincipalId ownerId, AgentCapability capability)
    {
        var grant = AgentGrant.Propose(tenantId, projectId, agentId, capability, null, Classification.Internal);
        grant.Approve(ownerId);
        return grant;
    }

    private sealed record Seed(
        Tenant Tenant,
        Project Project,
        Principal Owner,
        Principal Planner,
        Principal Executor,
        Principal ExecutorB,
        KnowledgeRevision RootRevision);
}
