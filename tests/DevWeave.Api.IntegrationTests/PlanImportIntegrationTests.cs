using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevWeave.Application.Abstractions;
using DevWeave.Contracts.Knowledge.V1;
using DevWeave.Contracts.Planning.V1;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Relations;
using DevWeave.Domain.Schema;
using DevWeave.Infrastructure.Persistence;
using DevWeave.Mcp.Protocol;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DevWeave.Api.IntegrationTests;

public sealed class PlanImportIntegrationTests : IClassFixture<IdentityTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private readonly IdentityTestFactory _factory;

    public PlanImportIntegrationTests(IdentityTestFactory factory)
    {
        _factory = factory;
        _factory.PrepareDatabase();
    }

    [Fact]
    public async Task Planning_agent_proposes_initial_plan_and_owner_materializes_it()
    {
        var seed = await SeedAsync(planning: true, execution: false);
        using var agentClient = _factory.CreateAuthenticatedAgentClient(seed.Tenant.Id.Value, seed.Planner.Id.Value);
        var url = $"/tenants/{seed.Tenant.Id.Value}/projects/{seed.Project.Id.Value}/mcp/rpc";

        var bundle = MinimalPlan($"import-{Guid.NewGuid():N}");
        using var propose = await agentClient.PostAsJsonAsync(url, new JsonRpcRequest("2.0", 1, McpMethods.CallTool,
            new { name = "propose_initial_plan", arguments = bundle }));
        Assert.Equal(HttpStatusCode.OK, propose.StatusCode);
        using var proposeDoc = await propose.Content.ReadFromJsonAsync<JsonDocument>();
        var proposalId = McpJson.Payload(proposeDoc!.RootElement).GetProperty("id").GetGuid();

        using var ownerClient = _factory.CreateAuthenticatedHumanClient(seed.Tenant.Id.Value, seed.Owner.Id.Value);
        var basePath = $"/tenants/{seed.Tenant.Id.Value}/projects/{seed.Project.Id.Value}/plan-import-proposals";
        using var listed = await ownerClient.GetAsync($"{basePath}/");
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        using var listDoc = await listed.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Contains(listDoc!.RootElement.EnumerateArray(), row => row.GetProperty("id").GetGuid() == proposalId);

        using var approve = await ownerClient.PostAsJsonAsync($"{basePath}/{proposalId}/approve",
            new DecideInitialPlanProposalRequest("Looks good"));
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        using var approved = await approve.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal("Approved", approved!.RootElement.GetProperty("status").GetString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
        Assert.Equal(2, await db.KnowledgeItems.CountAsync(i => i.TenantId == seed.Tenant.Id && i.ProjectId == seed.Project.Id));
        Assert.Equal(2, await db.PlanOutlineNodes.CountAsync(n => n.TenantId == seed.Tenant.Id && n.ProjectId == seed.Project.Id));
        Assert.Equal(1, await db.WorkItems.CountAsync(w => w.TenantId == seed.Tenant.Id && w.ProjectId == seed.Project.Id));
        Assert.Equal(1, await db.CodebaseTargets.CountAsync(t => t.TenantId == seed.Tenant.Id && t.ProjectId == seed.Project.Id));
        var target = await db.CodebaseTargets.SingleAsync(t => t.TenantId == seed.Tenant.Id && t.ProjectId == seed.Project.Id);
        Assert.Equal("github", target.Provider);
        Assert.Equal("acme/checkout", target.RepositoryId);
        Assert.Equal("main", target.TargetBranch);
    }

    [Fact]
    public async Task Execution_agent_cannot_propose_initial_plan_and_planning_agent_cannot_request_assignment()
    {
        var seed = await SeedAsync(planning: true, execution: true);
        using var planner = _factory.CreateAuthenticatedAgentClient(seed.Tenant.Id.Value, seed.Planner.Id.Value);
        using var executor = _factory.CreateAuthenticatedAgentClient(seed.Tenant.Id.Value, seed.Executor.Id.Value);
        var url = $"/tenants/{seed.Tenant.Id.Value}/projects/{seed.Project.Id.Value}/mcp/rpc";

        using var blockedPropose = await executor.PostAsJsonAsync(url, new JsonRpcRequest("2.0", 1, McpMethods.CallTool,
            new { name = "propose_initial_plan", arguments = MinimalPlan($"x-{Guid.NewGuid():N}") }));
        using var blockedProposeDoc = await blockedPropose.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(-32003, blockedProposeDoc!.RootElement.GetProperty("error").GetProperty("code").GetInt32());

        using var blockedLease = await planner.PostAsJsonAsync(url, new JsonRpcRequest("2.0", 2, McpMethods.CallTool,
            new
            {
                name = "request_assignment",
                arguments = new
                {
                    workPackageId = Guid.NewGuid(),
                    resourceScope = "src/",
                    expiresAt = DateTimeOffset.UtcNow.AddHours(1),
                    idempotencyKey = "lease-1",
                },
            }));
        using var blockedLeaseDoc = await blockedLease.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(-32003, blockedLeaseDoc!.RootElement.GetProperty("error").GetProperty("code").GetInt32());

        using var listPlanner = await planner.PostAsJsonAsync(url, new JsonRpcRequest("2.0", 3, McpMethods.ListTools, new { }));
        using var listPlannerDoc = await listPlanner.Content.ReadFromJsonAsync<JsonDocument>();
        var plannerTools = listPlannerDoc!.RootElement.GetProperty("result").GetProperty("tools")
            .EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToHashSet();
        Assert.Contains("propose_initial_plan", plannerTools);
        Assert.Contains("list_work_items", plannerTools);
        Assert.Contains("report_work_item", plannerTools);
        Assert.Contains("update_work_item_plan_state", plannerTools);
        Assert.DoesNotContain("request_assignment", plannerTools);

        using var listExecutor = await executor.PostAsJsonAsync(url, new JsonRpcRequest("2.0", 4, McpMethods.ListTools, new { }));
        using var listExecutorDoc = await listExecutor.Content.ReadFromJsonAsync<JsonDocument>();
        var executorTools = listExecutorDoc!.RootElement.GetProperty("result").GetProperty("tools")
            .EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToHashSet();
        Assert.Contains("request_assignment", executorTools);
        Assert.DoesNotContain("propose_initial_plan", executorTools);
        Assert.DoesNotContain("report_work_item", executorTools);
        Assert.DoesNotContain("update_work_item_plan_state", executorTools);
    }

    [Fact]
    public async Task Propose_initial_plan_tool_describes_import_procedure()
    {
        var seed = await SeedAsync(planning: true, execution: true);
        using var planner = _factory.CreateAuthenticatedAgentClient(seed.Tenant.Id.Value, seed.Planner.Id.Value);
        using var executor = _factory.CreateAuthenticatedAgentClient(seed.Tenant.Id.Value, seed.Executor.Id.Value);
        var url = $"/tenants/{seed.Tenant.Id.Value}/projects/{seed.Project.Id.Value}/mcp/rpc";

        using var listPlanner = await planner.PostAsJsonAsync(url, new JsonRpcRequest("2.0", 1, McpMethods.ListTools, new { }));
        using var listPlannerDoc = await listPlanner.Content.ReadFromJsonAsync<JsonDocument>();
        var propose = listPlannerDoc!.RootElement.GetProperty("result").GetProperty("tools")
            .EnumerateArray().Single(t => t.GetProperty("name").GetString() == "propose_initial_plan");
        var description = propose.GetProperty("description").GetString();
        Assert.Contains("list_schemas", description, StringComparison.Ordinal);
        Assert.Contains("Plan import", description, StringComparison.Ordinal);
        Assert.Contains("send_project_message", description, StringComparison.Ordinal);
        Assert.Contains("parentRef", description, StringComparison.Ordinal);
        Assert.Contains("there is no Done", description, StringComparison.Ordinal);
        Assert.Contains("work.completion_estimate", description, StringComparison.Ordinal);
        Assert.Contains("requireEmptyProject", description, StringComparison.Ordinal);
        Assert.Contains("one outline root", description, StringComparison.Ordinal);
        Assert.Contains("previous revision", description, StringComparison.Ordinal);

        var knowledgeRequired = propose.GetProperty("inputSchema").GetProperty("properties")
            .GetProperty("knowledge").GetProperty("items").GetProperty("required")
            .EnumerateArray().Select(v => v.GetString()).ToHashSet();
        Assert.Contains("ref", knowledgeRequired);
        Assert.Contains("typeId", knowledgeRequired);
        Assert.Contains("schemaVersion", knowledgeRequired);
        Assert.Contains("content", knowledgeRequired);

        var schemasTool = listPlannerDoc.RootElement.GetProperty("result").GetProperty("tools")
            .EnumerateArray().Single(t => t.GetProperty("name").GetString() == "list_schemas");
        Assert.Contains("propose_initial_plan", schemasTool.GetProperty("description").GetString(), StringComparison.Ordinal);

        using var listExecutor = await executor.PostAsJsonAsync(url, new JsonRpcRequest("2.0", 2, McpMethods.ListTools, new { }));
        using var listExecutorDoc = await listExecutor.Content.ReadFromJsonAsync<JsonDocument>();
        var send = listExecutorDoc!.RootElement.GetProperty("result").GetProperty("tools")
            .EnumerateArray().Single(t => t.GetProperty("name").GetString() == "send_project_message");
        Assert.Contains("propose_initial_plan", send.GetProperty("description").GetString(), StringComparison.Ordinal);
        Assert.Contains("Does not create Knowledge", send.GetProperty("description").GetString(), StringComparison.Ordinal);

        var reportTool = listPlannerDoc.RootElement.GetProperty("result").GetProperty("tools")
            .EnumerateArray().Single(t => t.GetProperty("name").GetString() == "report_work_item");
        Assert.Contains("WriteKnowledge", reportTool.GetProperty("description").GetString(), StringComparison.Ordinal);
        Assert.Contains("verify", reportTool.GetProperty("description").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("existing build", reportTool.GetProperty("description").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("done", reportTool.GetProperty("description").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("-32009", reportTool.GetProperty("inputSchema").GetProperty("properties")
            .GetProperty("expectedVersion").GetProperty("description").GetString(), StringComparison.Ordinal);

        var metricsTool = listPlannerDoc.RootElement.GetProperty("result").GetProperty("tools")
            .EnumerateArray().Single(t => t.GetProperty("name").GetString() == "get_project_metrics");
        Assert.Contains("work.completion_estimate", metricsTool.GetProperty("description").GetString(), StringComparison.Ordinal);
        Assert.Contains("evidence_backed_completion", metricsTool.GetProperty("description").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Planning_agent_updates_imported_work_item_status()
    {
        var seed = await SeedAsync(planning: true, execution: true);
        using var planner = _factory.CreateAuthenticatedAgentClient(seed.Tenant.Id.Value, seed.Planner.Id.Value);
        using var executor = _factory.CreateAuthenticatedAgentClient(seed.Tenant.Id.Value, seed.Executor.Id.Value);
        using var ownerClient = _factory.CreateAuthenticatedHumanClient(seed.Tenant.Id.Value, seed.Owner.Id.Value);
        var url = $"/tenants/{seed.Tenant.Id.Value}/projects/{seed.Project.Id.Value}/mcp/rpc";

        var bundle = MinimalPlan($"work-status-{Guid.NewGuid():N}");
        using var propose = await planner.PostAsJsonAsync(url, new JsonRpcRequest("2.0", 1, McpMethods.CallTool,
            new { name = "propose_initial_plan", arguments = bundle }));
        using var proposeDoc = await propose.Content.ReadFromJsonAsync<JsonDocument>();
        var proposalId = McpJson.Payload(proposeDoc!.RootElement).GetProperty("id").GetGuid();

        var basePath = $"/tenants/{seed.Tenant.Id.Value}/projects/{seed.Project.Id.Value}/plan-import-proposals";
        using var approve = await ownerClient.PostAsJsonAsync($"{basePath}/{proposalId}/approve",
            new DecideInitialPlanProposalRequest("Looks good"));
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        using var listed = await planner.PostAsJsonAsync(url, new JsonRpcRequest("2.0", 2, McpMethods.CallTool,
            new { name = "list_work_items", arguments = new { } }));
        using var listedDoc = await listed.Content.ReadFromJsonAsync<JsonDocument>();
        var workItems = McpJson.Payload(listedDoc!.RootElement).GetProperty("workItems");
        Assert.Equal(1, workItems.GetArrayLength());
        var item = workItems[0];
        Assert.Equal("Planned", item.GetProperty("plannedState").GetString());
        Assert.Equal("NotStarted", item.GetProperty("reportedState").GetString());
        Assert.Equal("Unverified", item.GetProperty("verificationState").GetString());
        Assert.Equal("Pending", item.GetProperty("approvalState").GetString());
        var key = item.GetProperty("key").GetString()!;

        using var illegalPlan = await planner.PostAsJsonAsync(url, new JsonRpcRequest("2.0", 3, McpMethods.CallTool,
            new
            {
                name = "update_work_item_plan_state",
                arguments = new { key, state = "InProgress", reason = "Skip ready" },
            }));
        using var illegalPlanDoc = await illegalPlan.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(-32009, illegalPlanDoc!.RootElement.GetProperty("error").GetProperty("code").GetInt32());

        using var ready = await planner.PostAsJsonAsync(url, new JsonRpcRequest("2.0", 4, McpMethods.CallTool,
            new
            {
                name = "update_work_item_plan_state",
                arguments = new { key, state = "Ready", reason = "Backlog triaged" },
            }));
        using var readyDoc = await ready.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal("Ready", McpJson.Payload(readyDoc!.RootElement).GetProperty("item").GetProperty("plannedState").GetString());

        using var done = await planner.PostAsJsonAsync(url, new JsonRpcRequest("2.0", 5, McpMethods.CallTool,
            new
            {
                name = "report_work_item",
                arguments = new { key, state = "done", reason = "Auth delivered in the demo app" },
            }));
        using var doneDoc = await done.Content.ReadFromJsonAsync<JsonDocument>();
        var reported = McpJson.Payload(doneDoc!.RootElement);
        Assert.Equal("Completed", reported.GetProperty("item").GetProperty("reportedState").GetString());
        Assert.Equal("Unverified", reported.GetProperty("item").GetProperty("verificationState").GetString());
        Assert.Equal("Pending", reported.GetProperty("item").GetProperty("approvalState").GetString());

        using var reopen = await planner.PostAsJsonAsync(url, new JsonRpcRequest("2.0", 6, McpMethods.CallTool,
            new
            {
                name = "report_work_item",
                arguments = new { key, state = "not-done", reason = "Reopened after review notes" },
            }));
        using var reopenDoc = await reopen.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal("InProgress", McpJson.Payload(reopenDoc!.RootElement).GetProperty("item").GetProperty("reportedState").GetString());

        using var blockedExecutor = await executor.PostAsJsonAsync(url, new JsonRpcRequest("2.0", 7, McpMethods.CallTool,
            new
            {
                name = "report_work_item",
                arguments = new { key, state = "Completed", reason = "Executor claim" },
            }));
        using var blockedExecutorDoc = await blockedExecutor.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(-32003, blockedExecutorDoc!.RootElement.GetProperty("error").GetProperty("code").GetInt32());

        using var verifyMissing = await planner.PostAsJsonAsync(url, new JsonRpcRequest("2.0", 8, McpMethods.CallTool,
            new { name = "verify_work_item", arguments = new { key, reason = "Nope" } }));
        using var verifyMissingDoc = await verifyMissing.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(-32601, verifyMissingDoc!.RootElement.GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task Planning_agent_registers_and_lists_git_codebase_targets_without_source()
    {
        var seed = await SeedAsync(planning: true, execution: true);
        using var planner = _factory.CreateAuthenticatedAgentClient(seed.Tenant.Id.Value, seed.Planner.Id.Value);
        using var executor = _factory.CreateAuthenticatedAgentClient(seed.Tenant.Id.Value, seed.Executor.Id.Value);
        using var owner = _factory.CreateAuthenticatedHumanClient(seed.Tenant.Id.Value, seed.Owner.Id.Value);
        var mcpUrl = $"/tenants/{seed.Tenant.Id.Value}/projects/{seed.Project.Id.Value}/mcp/rpc";
        var restUrl = $"/tenants/{seed.Tenant.Id.Value}/projects/{seed.Project.Id.Value}/coordination/codebase-targets";

        using var registered = await planner.PostAsJsonAsync(mcpUrl, new JsonRpcRequest("2.0", 1, McpMethods.CallTool,
            new
            {
                name = "register_codebase_target",
                arguments = new
                {
                    provider = "github",
                    repositoryId = "https://github.com/acme/checkout.git",
                    targetBranch = "main",
                    baseCommitSha = "Abc1234",
                },
            }));
        using var registeredDoc = await registered.Content.ReadFromJsonAsync<JsonDocument>();
        var target = McpJson.Payload(registeredDoc!.RootElement);
        Assert.Equal("github", target.GetProperty("provider").GetString());
        Assert.Equal("acme/checkout", target.GetProperty("repositoryId").GetString());
        Assert.Equal("abc1234", target.GetProperty("baseCommitSha").GetString());
        Assert.Equal("https://github.com/acme/checkout", target.GetProperty("browseUrl").GetString());
        Assert.DoesNotContain("class ", target.GetRawText(), StringComparison.Ordinal);

        using var again = await planner.PostAsJsonAsync(mcpUrl, new JsonRpcRequest("2.0", 2, McpMethods.CallTool,
            new
            {
                name = "register_codebase_target",
                arguments = new
                {
                    provider = "github",
                    repositoryId = "acme/checkout",
                    targetBranch = "main",
                    baseCommitSha = "abc1234",
                },
            }));
        using var againDoc = await again.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(target.GetProperty("id").GetGuid(), McpJson.Payload(againDoc!.RootElement).GetProperty("id").GetGuid());

        using var listed = await executor.PostAsJsonAsync(mcpUrl, new JsonRpcRequest("2.0", 3, McpMethods.CallTool,
            new { name = "list_codebase_targets", arguments = new { } }));
        using var listedDoc = await listed.Content.ReadFromJsonAsync<JsonDocument>();
        McpJson.AssertCallToolResult(listedDoc!.RootElement);
        Assert.Contains(
            McpJson.Payload(listedDoc.RootElement).GetProperty("codebaseTargets").EnumerateArray(),
            row => row.GetProperty("repositoryId").GetString() == "acme/checkout");

        using var rest = await owner.GetAsync(restUrl);
        Assert.Equal(HttpStatusCode.OK, rest.StatusCode);
        using var restDoc = await rest.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Contains(
            restDoc!.RootElement.EnumerateArray(),
            row => row.GetProperty("canonicalRef").GetString() == "github:acme/checkout@main#abc1234");

        using var blocked = await executor.PostAsJsonAsync(mcpUrl, new JsonRpcRequest("2.0", 4, McpMethods.CallTool,
            new
            {
                name = "register_codebase_target",
                arguments = new
                {
                    provider = "github",
                    repositoryId = "acme/other",
                    targetBranch = "main",
                    baseCommitSha = "def5678",
                },
            }));
        using var blockedDoc = await blocked.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(-32003, blockedDoc!.RootElement.GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task MCP_discovery_lists_schemas_relations_and_channels_with_role_enforcement()
    {
        var seed = await SeedAsync(planning: true, execution: true);
        Guid channelId;
        var relationCode = $"supports-{Guid.NewGuid():N}";
        using (var setup = _factory.Services.CreateScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
            var channel = CoordinationChannel.Open(
                seed.Tenant.Id, seed.Project.Id, "Campaign", "Internal");
            var relationType = RelationType.Create(relationCode, "Supports");
            db.AddRange(channel, relationType);
            await db.SaveChangesAsync();
            channelId = channel.Id.Value;
        }

        using var planner = _factory.CreateAuthenticatedAgentClient(seed.Tenant.Id.Value, seed.Planner.Id.Value);
        using var executor = _factory.CreateAuthenticatedAgentClient(seed.Tenant.Id.Value, seed.Executor.Id.Value);
        var mcpUrl = $"/tenants/{seed.Tenant.Id.Value}/projects/{seed.Project.Id.Value}/mcp/rpc";

        async Task<JsonElement> CallAsync(HttpClient client, int id, string method, object? parameters = null)
        {
            using var response = await client.PostAsJsonAsync(
                mcpUrl, new JsonRpcRequest("2.0", id, method, parameters));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var document = await response.Content.ReadFromJsonAsync<JsonDocument>();
            return document!.RootElement.Clone();
        }

        var resources = await CallAsync(planner, 1, McpMethods.ListResources, new { });
        Assert.Equal(0, resources.GetProperty("result").GetProperty("resources").GetArrayLength());
        Assert.Equal("public", resources.GetProperty("result").GetProperty("cacheScope").GetString());
        Assert.Equal("complete", resources.GetProperty("result").GetProperty("resultType").GetString());
        Assert.Equal(0, resources.GetProperty("result").GetProperty("ttlMs").GetInt32());

        var prompts = await CallAsync(planner, 2, McpMethods.ListPrompts, new { });
        Assert.Equal(0, prompts.GetProperty("result").GetProperty("prompts").GetArrayLength());
        Assert.Equal("complete", prompts.GetProperty("result").GetProperty("resultType").GetString());

        var missingResource = await CallAsync(
            planner, 3, McpMethods.ReadResource, new { uri = "devweave://missing" });
        Assert.Equal(-32004, missingResource.GetProperty("error").GetProperty("code").GetInt32());
        var missingPrompt = await CallAsync(
            planner, 4, McpMethods.GetPrompt, new { name = "missing" });
        Assert.Equal(-32004, missingPrompt.GetProperty("error").GetProperty("code").GetInt32());

        var schemas = await CallAsync(planner, 5, McpMethods.CallTool,
            new { name = "list_schemas", arguments = new { } });
        McpJson.AssertCallToolResult(schemas);
        Assert.Contains(
            McpJson.Payload(schemas).GetProperty("schemas").EnumerateArray(),
            schema => schema.GetProperty("typeId").GetString() == "doc.note" &&
                      schema.GetProperty("schemaVersion").GetString() == "1.0");

        var relationTypes = await CallAsync(planner, 6, McpMethods.CallTool,
            new { name = "list_relation_types", arguments = new { } });
        McpJson.AssertCallToolResult(relationTypes);
        Assert.Contains(
            McpJson.Payload(relationTypes).GetProperty("relationTypes").EnumerateArray(),
            relation => relation.GetProperty("code").GetString() == relationCode);

        var channels = await CallAsync(executor, 7, McpMethods.CallTool,
            new { name = "list_channels", arguments = new { } });
        McpJson.AssertCallToolResult(channels);
        Assert.Equal(JsonValueKind.Object, channels.GetProperty("result").ValueKind);
        Assert.Contains(
            channels.GetProperty("result").GetProperty("channels").EnumerateArray(),
            channel => channel.GetProperty("id").GetGuid() == channelId);
        Assert.Contains(
            McpJson.Payload(channels).GetProperty("channels").EnumerateArray(),
            channel => channel.GetProperty("id").GetGuid() == channelId);

        var messages = await CallAsync(executor, 8, McpMethods.CallTool,
            new { name = "list_messages", arguments = new { channelId, afterSequence = 0, limit = 10 } });
        McpJson.AssertCallToolResult(messages);
        Assert.Equal(JsonValueKind.Array, McpJson.Payload(messages).GetProperty("messages").ValueKind);

        var refused = await CallAsync(planner, 9, McpMethods.CallTool,
            new { name = "list_channels", arguments = new { } });
        Assert.Equal(-32003, refused.GetProperty("error").GetProperty("code").GetInt32());

        var restPath =
            $"/tenants/{seed.Tenant.Id.Value}/projects/{seed.Project.Id.Value}/coordination/channels";
        using var executorRest = await executor.GetAsync(restPath);
        Assert.Equal(HttpStatusCode.OK, executorRest.StatusCode);
        using var plannerRest = await planner.GetAsync(restPath);
        Assert.Equal(HttpStatusCode.Forbidden, plannerRest.StatusCode);
    }

    [Fact]
    public async Task Planning_agent_submits_knowledge_relation_milestone_and_work_for_owner_approval()
    {
        var seed = await SeedAsync(planning: true, execution: false);
        using var agent = _factory.CreateAuthenticatedAgentClient(seed.Tenant.Id.Value, seed.Planner.Id.Value);
        var url = McpUrl(seed);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var due = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);
        const string decision = """{"context":"Checkout","decision":"Use the ledger"}""";
        const string requirement = """{"description":"Checkout","acceptanceCriteria":[{"id":"AC-1","statement":"The buyer can pay"}]}""";

        using var proposed = await CallToolAsync(agent, url, 1, new
        {
            name = "propose_initial_plan",
            arguments = DesignGraph(suffix, due, decision, requirement),
        });
        var proposal = McpJson.Payload(proposed.RootElement);
        var proposalId = proposal.GetProperty("id").GetGuid();
        Assert.Equal("Proposed", proposal.GetProperty("status").GetString());

        using (var before = _factory.Services.CreateScope())
        {
            var db = before.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
            Assert.Equal(0, await db.KnowledgeItems.CountAsync(item => item.ProjectId == seed.Project.Id));
        }

        await ApproveImportAsync(seed, proposalId);

        using var after = _factory.Services.CreateScope();
        var saved = after.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
        var items = await saved.KnowledgeItems.Where(item => item.ProjectId == seed.Project.Id).ToListAsync();
        var decisionItem = Assert.Single(items, item => item.Key == $"ADR-{suffix}");
        var requirementItem = Assert.Single(items, item => item.Key == $"FR-{suffix}");
        Assert.Equal("architecture.decision", decisionItem.TypeId);
        Assert.Equal("requirements.functional", requirementItem.TypeId);
        Assert.Equal("Checkout decision", decisionItem.Title);
        Assert.Equal("Checkout", requirementItem.Title);

        var revisions = await saved.KnowledgeRevisions.Where(revision => revision.TenantId == seed.Tenant.Id).ToListAsync();
        Assert.Contains("Use the ledger", Assert.Single(revisions, revision => revision.Id == decisionItem.CurrentRevisionId).Content, StringComparison.Ordinal);
        Assert.Contains("AC-1", Assert.Single(revisions, revision => revision.Id == requirementItem.CurrentRevisionId).Content, StringComparison.Ordinal);

        var nodes = await saved.PlanOutlineNodes.Where(node => node.ProjectId == seed.Project.Id).ToListAsync();
        Assert.Null(Assert.Single(nodes, node => node.ItemId == decisionItem.Id).ParentItemId);
        Assert.Equal(decisionItem.Id, Assert.Single(nodes, node => node.ItemId == requirementItem.Id).ParentItemId);

        var edge = Assert.Single(await saved.Relationships.Where(row => row.ProjectId == seed.Project.Id).ToListAsync());
        Assert.Equal(decisionItem.Id, edge.SourceItemId);
        Assert.Equal(requirementItem.Id, edge.TargetItemId);

        var milestone = Assert.Single(await saved.Milestones.Where(row => row.ProjectId == seed.Project.Id).ToListAsync());
        Assert.Equal($"M-{suffix}", milestone.Key);
        Assert.Equal("Checkout release", milestone.Title);
        Assert.Equal(due, milestone.DueAt);

        var work = await saved.WorkItems.Where(row => row.ProjectId == seed.Project.Id).ToListAsync();
        var first = Assert.Single(work, row => row.Key == $"W1-{suffix}");
        var second = Assert.Single(work, row => row.Key == $"W2-{suffix}");
        Assert.Equal(milestone.Id, first.MilestoneId);
        Assert.Null(second.MilestoneId);
        Assert.Equal(WorkPriority.High, first.Priority);
        var dependency = Assert.Single(await saved.WorkItemDependencies.Where(row => row.ProjectId == seed.Project.Id).ToListAsync());
        Assert.Equal(second.Id, dependency.WorkItemId);
        Assert.Equal(first.Id, dependency.DependsOnId);

        using var outline = await CallToolAsync(agent, url, 2, new { name = "read_plan_outline", arguments = new { } });
        var link = Assert.Single(McpJson.Payload(outline.RootElement).GetProperty("links").EnumerateArray());
        Assert.Equal(first.Id.Value, link.GetProperty("workItemId").GetGuid());
        Assert.Equal(requirementItem.Id.Value, link.GetProperty("knowledgeItemId").GetGuid());
        Assert.Equal(requirementItem.CurrentRevisionId!.Value, link.GetProperty("revisionId").GetGuid());
    }

    [Fact]
    public async Task Propose_initial_plan_refuses_invalid_design_bundles()
    {
        var seed = await SeedAsync(planning: true, execution: false);
        var inactiveCode = $"retired-{Guid.NewGuid():N}";
        var draftNamespace = $"draft.{Guid.NewGuid():N}";
        using (var setup = _factory.Services.CreateScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
            var retired = RelationType.Create(inactiveCode, "Retired");
            retired.Deprecate();
            db.Add(retired);
            db.Add(SchemaDefinition.Create(draftNamespace, "1.0", "test", "Draft note", """{"type":"object"}"""));
            await db.SaveChangesAsync();
        }

        using var agent = _factory.CreateAuthenticatedAgentClient(seed.Tenant.Id.Value, seed.Planner.Id.Value);
        var url = McpUrl(seed);
        var rootOutline = new object[] { new { itemRef = "root", parentRef = (string?)null, position = 0 } };
        var pairOutline = new object[]
        {
            new { itemRef = "root", parentRef = (string?)null, position = 0 },
            new { itemRef = "child", parentRef = "root", position = 0 },
        };

        await AssertRefusedAsync(agent, url, 1,
            Core($"unknown-{Guid.NewGuid():N}", new[] { Note("root"), Note("child") }, pairOutline,
                relations: new object[] { new { sourceRef = "root", targetRef = "child", relationTypeCode = $"missing-{Guid.NewGuid():N}" } }),
            -32004, "not-found", "not registered");
        await AssertRefusedAsync(agent, url, 2,
            Core($"inactive-{Guid.NewGuid():N}", new[] { Note("root"), Note("child") }, pairOutline,
                relations: new object[] { new { sourceRef = "root", targetRef = "child", relationTypeCode = inactiveCode } }),
            -32009, "conflict", "not active");
        await AssertRefusedAsync(agent, url, 3,
            Core($"endpoint-{Guid.NewGuid():N}", new[] { Note("root"), Note("child") }, pairOutline,
                relations: new object[] { new { sourceRef = "root", targetRef = "child", relationTypeCode = "satisfies" } }),
            -32602, "invalid", "does not allow");
        await AssertRefusedAsync(agent, url, 4,
            Core($"milestone-{Guid.NewGuid():N}", new[] { Note("root") }, rootOutline,
                workItems: new object[]
                {
                    new { @ref = "w1", key = $"w-{Guid.NewGuid():N}"[..12], title = "Build", description = "Deliver", milestoneRef = "missing", priority = "Normal", dependsOnRefs = Array.Empty<string>() },
                }),
            -32602, "invalid", "milestoneRef");
        await AssertRefusedAsync(agent, url, 5,
            Core($"source-{Guid.NewGuid():N}", new[] { Note("root", representationKind: "SourceCode") }, rootOutline),
            -32602, "invalid", "SourceCode");
        await AssertRefusedAsync(agent, url, 6,
            Core($"ceiling-{Guid.NewGuid():N}", new[] { Note("root", classification: "Restricted") }, rootOutline),
            -32003, "forbidden", "classification ceiling");
        await AssertRefusedAsync(agent, url, 7,
            Core($"draft-{Guid.NewGuid():N}", new[] { Note("root", typeId: draftNamespace) }, rootOutline),
            -32004, "not-found", "No active schema");
        await AssertRefusedAsync(agent, url, 8,
            Core($"shape-{Guid.NewGuid():N}", new[] { Note("root", typeId: "requirements.functional", content: """{"summary":"Checkout"}""") }, rootOutline),
            -32602, "invalid", "JSON Schema");

        using var check = _factory.Services.CreateScope();
        var unchanged = check.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
        Assert.Equal(0, await unchanged.KnowledgeItems.CountAsync(item => item.ProjectId == seed.Project.Id));
    }

    [Fact]
    public async Task Repeated_plan_import_keeps_one_proposal_and_refuses_a_filled_outline()
    {
        var seed = await SeedAsync(planning: true, execution: false);
        using var agent = _factory.CreateAuthenticatedAgentClient(seed.Tenant.Id.Value, seed.Planner.Id.Value);
        var url = McpUrl(seed);
        var key = $"once-{Guid.NewGuid():N}";
        var bundle = Core(key, new[] { Note("root", content: """{"summary":"Checkout"}""") },
            new object[] { new { itemRef = "root", parentRef = (string?)null, position = 0 } });
        var changed = Core(key, new[] { Note("root", content: """{"summary":"Billing"}""") },
            new object[] { new { itemRef = "root", parentRef = (string?)null, position = 0 } });

        using var first = await CallToolAsync(agent, url, 1, new { name = "propose_initial_plan", arguments = bundle });
        var proposalId = McpJson.Payload(first.RootElement).GetProperty("id").GetGuid();
        using var again = await CallToolAsync(agent, url, 2, new { name = "propose_initial_plan", arguments = bundle });
        Assert.Equal(proposalId, McpJson.Payload(again.RootElement).GetProperty("id").GetGuid());
        Assert.Equal("Proposed", McpJson.Payload(again.RootElement).GetProperty("status").GetString());

        await AssertRefusedAsync(agent, url, 3, changed, -32009, "conflict", "different plan payload");
        await ApproveImportAsync(seed, proposalId);
        await AssertRefusedAsync(agent, url, 4, bundle, -32009, "conflict", "decided plan import");
        await AssertRefusedAsync(agent, url, 5,
            Core($"next-{Guid.NewGuid():N}", new[] { Note("root") },
                new object[] { new { itemRef = "root", parentRef = (string?)null, position = 0 } }),
            -32009, "conflict", "not empty");
    }

    [Fact]
    public async Task Design_discovery_lists_canonical_schema_and_satisfies_endpoints()
    {
        var seed = await SeedAsync(planning: true, execution: false);
        PrincipalId readerId;
        using (var setup = _factory.Services.CreateScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
            var reader = Principal.Create(seed.Tenant.Id, $"reader-{Guid.NewGuid():N}@example.test", PrincipalType.AiAgent);
            db.Add(reader);
            db.Add(ProjectMembership.Grant(seed.Tenant.Id, seed.Project.Id, reader.Id, ProjectRole.Member));
            db.Add(AgentCredential.Issue(seed.Tenant.Id, reader.Id, "test", "hashed-reader", null));
            var grant = AgentGrant.Propose(seed.Tenant.Id, seed.Project.Id, reader.Id, AgentCapability.ReadKnowledge, null, Classification.Internal);
            grant.Approve(seed.Owner.Id);
            db.Add(grant);
            await db.SaveChangesAsync();
            readerId = reader.Id;
        }

        using var planner = _factory.CreateAuthenticatedAgentClient(seed.Tenant.Id.Value, seed.Planner.Id.Value);
        using var readerClient = _factory.CreateAuthenticatedAgentClient(seed.Tenant.Id.Value, readerId.Value);
        var url = McpUrl(seed);

        using var schemas = await CallToolAsync(planner, url, 1, new { name = "list_schemas", arguments = new { } });
        var functional = Assert.Single(
            McpJson.Payload(schemas.RootElement).GetProperty("schemas").EnumerateArray(),
            schema => schema.GetProperty("typeId").GetString() == "requirements.functional");
        Assert.Equal("1.0", functional.GetProperty("schemaVersion").GetString());
        Assert.True(functional.GetProperty("isCanonical").GetBoolean());

        using var relations = await CallToolAsync(planner, url, 2, new { name = "list_relation_types", arguments = new { } });
        var satisfies = Assert.Single(
            McpJson.Payload(relations.RootElement).GetProperty("relationTypes").EnumerateArray(),
            relation => relation.GetProperty("code").GetString() == "satisfies");
        Assert.Contains(satisfies.GetProperty("allowedSourceTypeIds").EnumerateArray(), type => type.GetString() == "architecture.decision");
        Assert.Contains(satisfies.GetProperty("allowedTargetTypeIds").EnumerateArray(), type => type.GetString() == "requirements.functional");

        using var listed = await readerClient.PostAsJsonAsync(url, new JsonRpcRequest("2.0", 3, McpMethods.ListTools, new { }));
        using var listedDoc = await listed.Content.ReadFromJsonAsync<JsonDocument>();
        var names = listedDoc!.RootElement.GetProperty("result").GetProperty("tools")
            .EnumerateArray().Select(tool => tool.GetProperty("name").GetString()).ToHashSet();
        Assert.Contains("list_schemas", names);
        Assert.DoesNotContain("list_relation_types", names);
        await AssertRefusedAsync(readerClient, url, 4, new { }, -32003, "forbidden", "list_relation_types", "list_relation_types");
    }

    [Fact]
    public async Task Plan_expansion_stays_a_proposal_until_a_human_accepts_the_child()
    {
        var seed = await SeedAsync(planning: true, execution: false);
        using var agent = _factory.CreateAuthenticatedAgentClient(seed.Tenant.Id.Value, seed.Planner.Id.Value);
        var url = McpUrl(seed);
        var bundle = Core($"expand-{Guid.NewGuid():N}", new[] { Note("root", content: """{"summary":"Checkout"}""") },
            new object[] { new { itemRef = "root", parentRef = (string?)null, position = 0 } });
        using var proposed = await CallToolAsync(agent, url, 1, new { name = "propose_initial_plan", arguments = bundle });
        await ApproveImportAsync(seed, McpJson.Payload(proposed.RootElement).GetProperty("id").GetGuid());

        Guid rootId;
        using (var before = _factory.Services.CreateScope())
        {
            var db = before.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
            rootId = (await db.PlanOutlineNodes.SingleAsync(node => node.ProjectId == seed.Project.Id)).ItemId.Value;
            Assert.Equal(1, await db.KnowledgeItems.CountAsync(item => item.ProjectId == seed.Project.Id));
        }

        using var expansion = await CallToolAsync(agent, url, 2, new
        {
            name = "propose_plan_expansion",
            arguments = new
            {
                parentItemId = rootId,
                key = "child-module",
                title = "Child module",
                typeId = "doc.note",
                rationale = "Split checkout",
            },
        });
        var expansionId = McpJson.Payload(expansion.RootElement).GetProperty("id").GetGuid();
        Assert.Equal("Proposed", McpJson.Payload(expansion.RootElement).GetProperty("status").GetString());

        using (var held = _factory.Services.CreateScope())
        {
            var db = held.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
            Assert.Equal(1, await db.KnowledgeItems.CountAsync(item => item.ProjectId == seed.Project.Id));
            Assert.Equal(1, await db.PlanOutlineNodes.CountAsync(node => node.ProjectId == seed.Project.Id));
        }

        using var owner = _factory.CreateAuthenticatedHumanClient(seed.Tenant.Id.Value, seed.Owner.Id.Value);
        var tenant = seed.Tenant.Id.Value;
        using var created = await owner.PostAsJsonAsync(
            $"/tenants/{tenant}/projects/{seed.Project.Id.Value}/knowledge",
            new CreateKnowledgeItemRequest("child-module", "Child module", Classification.Internal, RepresentationKind.Text, "doc.note", "1.0", seed.Owner.Id.Value),
            JsonOptions);
        var child = await ReadOkAsync<KnowledgeItemResponse>(created, HttpStatusCode.Created);
        using var revised = await owner.PostAsJsonAsync(
            $"/tenants/{tenant}/knowledge/{child.Id}/revisions",
            new AddRevisionRequest(0, "1.0", """{"summary":"Split checkout"}""", seed.Owner.Id.Value, "Accepted child", null, null),
            JsonOptions);
        var revision = await ReadOkAsync<KnowledgeRevisionResponse>(revised, HttpStatusCode.Created);
        using var approved = await owner.PostAsJsonAsync(
            $"/tenants/{tenant}/knowledge/{child.Id}/approve",
            new ReviewRevisionRequest(revision.Id, "Reviewed"),
            JsonOptions);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);

        using (var createdOnly = _factory.Services.CreateScope())
        {
            var db = createdOnly.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
            Assert.Equal(2, await db.KnowledgeItems.CountAsync(item => item.ProjectId == seed.Project.Id));
            Assert.Equal(1, await db.PlanOutlineNodes.CountAsync(node => node.ProjectId == seed.Project.Id));
        }

        using var decided = await owner.PostAsJsonAsync(
            $"/tenants/{tenant}/projects/{seed.Project.Id.Value}/plan/proposals/{expansionId}/decide",
            new { acceptedItemId = child.Id },
            JsonOptions);
        using var decision = await ReadOkAsync<JsonDocument>(decided, HttpStatusCode.OK);
        Assert.Equal("Accepted", decision.RootElement.GetProperty("status").GetString());

        using var outline = await CallToolAsync(agent, url, 3, new { name = "read_plan_outline", arguments = new { } });
        var childNode = Assert.Single(
            McpJson.Payload(outline.RootElement).GetProperty("nodes").EnumerateArray(),
            node => node.GetProperty("itemId").GetGuid() == child.Id);
        Assert.Equal(rootId, childNode.GetProperty("parentItemId").GetGuid());
    }

    [Fact]
    public async Task Changing_a_requirement_leaves_the_work_link_on_the_previous_revision()
    {
        var seed = await SeedAsync(planning: true, execution: false);
        using var agent = _factory.CreateAuthenticatedAgentClient(seed.Tenant.Id.Value, seed.Planner.Id.Value);
        using var owner = _factory.CreateAuthenticatedHumanClient(seed.Tenant.Id.Value, seed.Owner.Id.Value);
        var url = McpUrl(seed);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        using var proposed = await CallToolAsync(agent, url, 1, new
        {
            name = "propose_initial_plan",
            arguments = DesignGraph(
                suffix,
                new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero),
                """{"context":"Checkout","decision":"Use the ledger"}""",
                """{"description":"Checkout","acceptanceCriteria":[{"id":"AC-1","statement":"The buyer can pay"}]}"""),
        });
        await ApproveImportAsync(seed, McpJson.Payload(proposed.RootElement).GetProperty("id").GetGuid());

        using var outline = await CallToolAsync(agent, url, 2, new { name = "read_plan_outline", arguments = new { } });
        var link = Assert.Single(McpJson.Payload(outline.RootElement).GetProperty("links").EnumerateArray());
        var pinnedRevisionId = link.GetProperty("revisionId").GetGuid();
        var requirementId = link.GetProperty("knowledgeItemId").GetGuid();

        using var listed = await agent.PostAsJsonAsync(url, new JsonRpcRequest("2.0", 3, McpMethods.ListTools, new { }));
        using var listedDoc = await listed.Content.ReadFromJsonAsync<JsonDocument>();
        var names = listedDoc!.RootElement.GetProperty("result").GetProperty("tools")
            .EnumerateArray().Select(tool => tool.GetProperty("name").GetString()).ToHashSet();
        Assert.DoesNotContain("add_revision", names);
        Assert.DoesNotContain("revise_requirement", names);
        await AssertRefusedAsync(agent, url, 4, new { knowledgeItemId = requirementId }, -32601, "unknown-tool", "Unknown tool", "add_revision");

        using var revised = await owner.PostAsJsonAsync(
            $"/tenants/{seed.Tenant.Id.Value}/knowledge/{requirementId}/revisions",
            new AddRevisionRequest(
                1, "1.0",
                """{"description":"Checkout revised","acceptanceCriteria":[{"id":"AC-1","statement":"The buyer can pay by card"}]}""",
                seed.Owner.Id.Value, "Tighten checkout", null, null),
            JsonOptions);
        var revision = await ReadOkAsync<KnowledgeRevisionResponse>(revised, HttpStatusCode.Created);
        Assert.NotEqual(pinnedRevisionId, revision.Id);

        using var staleApproval = await owner.PostAsJsonAsync(
            $"/tenants/{seed.Tenant.Id.Value}/knowledge/{requirementId}/approve",
            new ReviewRevisionRequest(pinnedRevisionId, "Old text"));
        Assert.Equal(HttpStatusCode.Conflict, staleApproval.StatusCode);

        using var stillPinned = await CallToolAsync(agent, url, 5, new { name = "read_plan_outline", arguments = new { } });
        Assert.Equal(pinnedRevisionId, Assert.Single(McpJson.Payload(stillPinned.RootElement).GetProperty("links").EnumerateArray()).GetProperty("revisionId").GetGuid());

        await AssertRefusedAsync(agent, url, 6, new
        {
            parentItemId = requirementId,
            key = "checkout-child",
            title = "Checkout child",
            typeId = "requirements.functional",
            rationale = "Split the revised requirement",
        }, -32009, "conflict", "human-reviewed", "propose_plan_expansion");

        using var approved = await owner.PostAsJsonAsync(
            $"/tenants/{seed.Tenant.Id.Value}/knowledge/{requirementId}/approve",
            new ReviewRevisionRequest(revision.Id, "Reviewed the revision"));
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);

        using var expansion = await CallToolAsync(agent, url, 7, new
        {
            name = "propose_plan_expansion",
            arguments = new
            {
                parentItemId = requirementId,
                key = "checkout-child",
                title = "Checkout child",
                typeId = "requirements.functional",
                rationale = "Split the revised requirement",
            },
        });
        Assert.Equal("Proposed", McpJson.Payload(expansion.RootElement).GetProperty("status").GetString());

        using var after = _factory.Services.CreateScope();
        var db = after.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
        var requirement = await db.KnowledgeItems.SingleAsync(item => item.Id == new KnowledgeItemId(requirementId));
        Assert.Equal(revision.Id, requirement.CurrentRevisionId!.Value);
        Assert.Equal(2, await db.KnowledgeRevisions.CountAsync(row => row.KnowledgeItemId == requirement.Id));
        Assert.Equal(pinnedRevisionId, Assert.Single(await db.PlanWorkLinks.Where(row => row.ProjectId == seed.Project.Id).ToListAsync()).RevisionId.Value);
        Assert.Equal(2, await db.KnowledgeItems.CountAsync(item => item.ProjectId == seed.Project.Id));
    }

    [Fact]
    public async Task A_second_import_cannot_add_another_outline_root()
    {
        var seed = await SeedAsync(planning: true, execution: false);
        using var agent = _factory.CreateAuthenticatedAgentClient(seed.Tenant.Id.Value, seed.Planner.Id.Value);
        var url = McpUrl(seed);
        var outline = new object[] { new { itemRef = "root", parentRef = (string?)null, position = 0 } };
        var first = Core($"root-{Guid.NewGuid():N}", new[] { Note("root", content: """{"summary":"Checkout"}""") }, outline);
        using var proposed = await CallToolAsync(agent, url, 1, new { name = "propose_initial_plan", arguments = first });
        await ApproveImportAsync(seed, McpJson.Payload(proposed.RootElement).GetProperty("id").GetGuid());

        var second = Core(
            $"child-{Guid.NewGuid():N}",
            new[] { Note("root", content: """{"summary":"Billing"}""") },
            outline,
            requireEmptyProject: false);
        using var appended = await CallToolAsync(agent, url, 2, new { name = "propose_initial_plan", arguments = second });
        var proposalId = McpJson.Payload(appended.RootElement).GetProperty("id").GetGuid();
        Assert.Equal("Proposed", McpJson.Payload(appended.RootElement).GetProperty("status").GetString());

        using var owner = _factory.CreateAuthenticatedHumanClient(seed.Tenant.Id.Value, seed.Owner.Id.Value);
        using var approve = await owner.PostAsJsonAsync(
            $"/tenants/{seed.Tenant.Id.Value}/projects/{seed.Project.Id.Value}/plan-import-proposals/{proposalId}/approve",
            new DecideInitialPlanProposalRequest("Append"));
        Assert.Equal(HttpStatusCode.Conflict, approve.StatusCode);
        Assert.Contains("one outline root", await approve.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
        Assert.Equal(1, await db.PlanOutlineNodes.CountAsync(node => node.ProjectId == seed.Project.Id));
        var original = Assert.Single(await db.KnowledgeRevisions.Where(row => row.TenantId == seed.Tenant.Id && row.RevisionNumber == 1).ToListAsync(),
            row => row.Content.Contains("Checkout", StringComparison.Ordinal));
        Assert.Contains("Checkout", original.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stale_work_report_version_is_refused()
    {
        var seed = await SeedAsync(planning: true, execution: false);
        using var agent = _factory.CreateAuthenticatedAgentClient(seed.Tenant.Id.Value, seed.Planner.Id.Value);
        var url = McpUrl(seed);
        var bundle = Core($"version-{Guid.NewGuid():N}", new[] { Note("root") },
            new object[] { new { itemRef = "root", parentRef = (string?)null, position = 0 } },
            workItems: new object[]
            {
                new
                {
                    @ref = "w1",
                    key = $"W-{Guid.NewGuid():N}"[..12],
                    title = "Deliver checkout",
                    description = "Ship the slice",
                    milestoneRef = (string?)null,
                    priority = "Normal",
                    dependsOnRefs = Array.Empty<string>(),
                },
            });
        using var proposed = await CallToolAsync(agent, url, 1, new { name = "propose_initial_plan", arguments = bundle });
        await ApproveImportAsync(seed, McpJson.Payload(proposed.RootElement).GetProperty("id").GetGuid());

        using var listed = await CallToolAsync(agent, url, 2, new { name = "list_work_items", arguments = new { } });
        var item = Assert.Single(McpJson.Payload(listed.RootElement).GetProperty("workItems").EnumerateArray());
        var version = item.GetProperty("version").GetInt32();
        var key = item.GetProperty("key").GetString();
        Assert.Equal("NotStarted", item.GetProperty("reportedState").GetString());

        await AssertRefusedAsync(agent, url, 3, new
        {
            key,
            state = "done",
            reason = "Already built",
            expectedVersion = version + 5,
        }, -32009, "conflict", "version", "report_work_item");

        using var reported = await CallToolAsync(agent, url, 4, new
        {
            name = "report_work_item",
            arguments = new { key, state = "InProgress", reason = "Part of the slice exists", expectedVersion = version },
        });
        var partial = McpJson.Payload(reported.RootElement).GetProperty("item");
        Assert.Equal("InProgress", partial.GetProperty("reportedState").GetString());
        Assert.Equal("Unverified", partial.GetProperty("verificationState").GetString());
        Assert.Equal(version + 1, partial.GetProperty("version").GetInt32());

        using var done = await CallToolAsync(agent, url, 5, new
        {
            name = "report_work_item",
            arguments = new { key, state = "done", reason = "Slice is in git", expectedVersion = partial.GetProperty("version").GetInt32() },
        });
        Assert.Equal("Completed", McpJson.Payload(done.RootElement).GetProperty("item").GetProperty("reportedState").GetString());
    }

    [Fact]
    public async Task A_done_status_on_import_stays_unstarted_until_a_later_report()
    {
        var seed = await SeedAsync(planning: true, execution: false);
        using var agent = _factory.CreateAuthenticatedAgentClient(seed.Tenant.Id.Value, seed.Planner.Id.Value);
        var url = McpUrl(seed);
        var builtKey = $"B-{Guid.NewGuid():N}"[..12];
        var openKey = $"O-{Guid.NewGuid():N}"[..12];
        var bundle = Core($"mix-{Guid.NewGuid():N}", new[] { Note("root") },
            new object[] { new { itemRef = "root", parentRef = (string?)null, position = 0 } },
            workItems: new object[]
            {
                new
                {
                    @ref = "built",
                    key = builtKey,
                    title = "Checkout already in git",
                    description = "Existing slice",
                    milestoneRef = (string?)null,
                    priority = "Normal",
                    dependsOnRefs = Array.Empty<string>(),
                    status = "Done",
                },
                new
                {
                    @ref = "open",
                    key = openKey,
                    title = "Billing still to build",
                    description = "Unstarted slice",
                    milestoneRef = (string?)null,
                    priority = "Normal",
                    dependsOnRefs = Array.Empty<string>(),
                },
            });
        using var proposed = await CallToolAsync(agent, url, 1, new { name = "propose_initial_plan", arguments = bundle });
        await ApproveImportAsync(seed, McpJson.Payload(proposed.RootElement).GetProperty("id").GetGuid());

        using var listed = await CallToolAsync(agent, url, 2, new { name = "list_work_items", arguments = new { } });
        var items = McpJson.Payload(listed.RootElement).GetProperty("workItems").EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        foreach (var item in items)
        {
            Assert.Equal("Planned", item.GetProperty("plannedState").GetString());
            Assert.Equal("NotStarted", item.GetProperty("reportedState").GetString());
            Assert.Equal("Unverified", item.GetProperty("verificationState").GetString());
            Assert.Equal("Pending", item.GetProperty("approvalState").GetString());
        }

        var built = items.Single(item => item.GetProperty("key").GetString() == builtKey);
        using var reported = await CallToolAsync(agent, url, 3, new
        {
            name = "report_work_item",
            arguments = new
            {
                key = builtKey,
                state = "done",
                reason = "That slice is already in git",
                expectedVersion = built.GetProperty("version").GetInt32(),
            },
        });
        Assert.Equal("Completed", McpJson.Payload(reported.RootElement).GetProperty("item").GetProperty("reportedState").GetString());
        Assert.Equal("Unverified", McpJson.Payload(reported.RootElement).GetProperty("item").GetProperty("verificationState").GetString());

        using var metricsCall = await CallToolAsync(agent, url, 4, new { name = "get_project_metrics", arguments = new { } });
        var metrics = McpJson.Payload(metricsCall.RootElement).GetProperty("metrics");
        JsonElement Metric(string key) => metrics.EnumerateArray().Single(row => row.GetProperty("key").GetString() == key);

        var estimate = Metric("work.completion_estimate");
        Assert.Equal(0.5m, estimate.GetProperty("value").GetDecimal());
        Assert.True(estimate.GetProperty("isEstimate").GetBoolean());
        Assert.Equal(1m, Metric("work.reported_completed_count").GetProperty("value").GetDecimal());

        var backed = Metric("closedloop.evidence_backed_completion");
        Assert.Equal(0m, backed.GetProperty("value").GetDecimal());
        Assert.False(backed.GetProperty("isEstimate").GetBoolean());
        Assert.Contains("Numerator 0, denominator 1", backed.GetProperty("includedPopulation").GetString(), StringComparison.Ordinal);

        using var after = await CallToolAsync(agent, url, 5, new { name = "list_work_items", arguments = new { } });
        var remaining = McpJson.Payload(after.RootElement).GetProperty("workItems").EnumerateArray()
            .Single(item => item.GetProperty("key").GetString() == openKey);
        Assert.Equal("NotStarted", remaining.GetProperty("reportedState").GetString());
    }

    [Fact]
    public async Task Agent_proposer_cannot_approve_plan_import()
    {
        var seed = await SeedAsync(planning: true, execution: false);
        using var scope = _factory.Services.CreateScope();
        var import = scope.ServiceProvider.GetRequiredService<Application.Planning.PlanImportService>();
        var proposal = await import.ProposeAsync(seed.Tenant.Id, seed.Project.Id, seed.Planner.Id,
            ToContract(MinimalPlan($"self-{Guid.NewGuid():N}")), CancellationToken.None);

        await Assert.ThrowsAsync<UnauthorizedTenantException>(() =>
            import.ApproveAsync(seed.Tenant.Id, seed.Project.Id, seed.Planner.Id, proposal.Id, null, CancellationToken.None));
    }

    private async Task<Seed> SeedAsync(bool planning, bool execution)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
        var tenant = Tenant.Create($"Import {Guid.NewGuid():N}");
        var owner = Principal.Create(tenant.Id, $"owner-{Guid.NewGuid():N}@example.test", PrincipalType.Human);
        var planner = Principal.Create(tenant.Id, $"planner-{Guid.NewGuid():N}@example.test", PrincipalType.AiAgent);
        var executor = Principal.Create(tenant.Id, $"executor-{Guid.NewGuid():N}@example.test", PrincipalType.AiAgent);
        var profile = LifecycleProfile.Create(tenant.Id, Guid.NewGuid().ToString("N"), "Plan", "1");
        var project = Project.Create(tenant.Id, "App", Guid.NewGuid().ToString("N"), null, profile.Id, owner.Id);
        db.AddRange(tenant, owner, planner, executor, profile, project);
        db.Add(ProjectMembership.Grant(tenant.Id, project.Id, owner.Id, ProjectRole.Owner));

        var schema = SchemaDefinition.Create("doc.note", "1.0", "platform", "Note", "{\"type\":\"object\"}");
        schema.Publish();
        if (!await db.SchemaDefinitions.AnyAsync(s => s.Namespace == "doc.note" && s.Version == "1.0"))
            db.Add(schema);

        void Grant(Principal agent, params AgentCapability[] capabilities)
        {
            db.Add(ProjectMembership.Grant(tenant.Id, project.Id, agent.Id, ProjectRole.Member));
            db.Add(AgentCredential.Issue(tenant.Id, agent.Id, "test", "hashed-test-secret", null));
            foreach (var capability in capabilities)
            {
                var grant = AgentGrant.Propose(tenant.Id, project.Id, agent.Id, capability, null, Classification.Internal);
                grant.Approve(owner.Id);
                db.Add(grant);
            }
        }

        if (planning)
            Grant(planner, AgentCapability.ReadKnowledge, AgentCapability.WriteKnowledge);
        if (execution)
            Grant(executor, AgentCapability.ReadKnowledge, AgentCapability.ExecuteWorkflow, AgentCapability.AssembleContext);

        await db.SaveChangesAsync();
        return new Seed(tenant, project, owner, planner, executor);
    }

    private static string McpUrl(Seed seed) =>
        $"/tenants/{seed.Tenant.Id.Value}/projects/{seed.Project.Id.Value}/mcp/rpc";

    private async Task ApproveImportAsync(Seed seed, Guid proposalId)
    {
        using var owner = _factory.CreateAuthenticatedHumanClient(seed.Tenant.Id.Value, seed.Owner.Id.Value);
        using var approve = await owner.PostAsJsonAsync(
            $"/tenants/{seed.Tenant.Id.Value}/projects/{seed.Project.Id.Value}/plan-import-proposals/{proposalId}/approve",
            new DecideInitialPlanProposalRequest("Looks good"));
        if (approve.StatusCode != HttpStatusCode.OK)
            throw new HttpRequestException($"{(int)approve.StatusCode} {await approve.Content.ReadAsStringAsync()}");
    }

    private static async Task<JsonDocument> CallToolAsync(HttpClient client, string url, int id, object parameters)
    {
        using var response = await client.PostAsJsonAsync(url, new JsonRpcRequest("2.0", id, McpMethods.CallTool, parameters));
        if (response.StatusCode != HttpStatusCode.OK)
            throw new HttpRequestException($"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<JsonDocument>())!;
    }

    private static async Task AssertRefusedAsync(
        HttpClient client,
        string url,
        int id,
        object arguments,
        int code,
        string kind,
        string message,
        string tool = "propose_initial_plan")
    {
        using var document = await CallToolAsync(client, url, id, new { name = tool, arguments });
        var error = document.RootElement.GetProperty("error");
        Assert.Equal(code, error.GetProperty("code").GetInt32());
        Assert.Equal(kind, error.GetProperty("data").GetProperty("kind").GetString());
        Assert.Contains(message, error.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<T> ReadOkAsync<T>(HttpResponseMessage response, HttpStatusCode expected)
    {
        if (response.StatusCode != expected)
            throw new HttpRequestException($"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private static object DesignGraph(string suffix, DateTimeOffset due, string decision, string requirement) => new
    {
        idempotencyKey = $"design-{suffix}",
        requireEmptyProject = true,
        knowledge = new object[]
        {
            new
            {
                @ref = "adr",
                key = $"ADR-{suffix}",
                title = "Checkout decision",
                typeId = "architecture.decision",
                schemaVersion = "1.0",
                classification = "Internal",
                representationKind = "StructuredData",
                content = decision,
                reason = "Record the checkout decision",
            },
            new
            {
                @ref = "req",
                key = $"FR-{suffix}",
                title = "Checkout",
                typeId = "requirements.functional",
                schemaVersion = "1.0",
                classification = "Internal",
                representationKind = "StructuredData",
                content = requirement,
                reason = "Record the checkout requirement",
            },
        },
        outline = new object[]
        {
            new { itemRef = "adr", parentRef = (string?)null, position = 0 },
            new { itemRef = "req", parentRef = "adr", position = 0 },
        },
        relations = new object[]
        {
            new { sourceRef = "adr", targetRef = "req", relationTypeCode = "satisfies" },
        },
        milestones = new object[]
        {
            new { @ref = "m1", key = $"M-{suffix}", title = "Checkout release", dueAt = due },
        },
        workItems = new object[]
        {
            new
            {
                @ref = "w1",
                key = $"W1-{suffix}",
                title = "Deliver checkout",
                description = "Ship the ledger path",
                milestoneRef = "m1",
                priority = "High",
                dependsOnRefs = Array.Empty<string>(),
            },
            new
            {
                @ref = "w2",
                key = $"W2-{suffix}",
                title = "Review checkout",
                description = "Review the ledger path",
                milestoneRef = (string?)null,
                priority = "Normal",
                dependsOnRefs = new[] { "w1" },
            },
        },
        workLinks = new object[]
        {
            new { workItemRef = "w1", knowledgeRef = "req" },
        },
    };

    private static object Note(
        string @ref,
        string typeId = "doc.note",
        string schemaVersion = "1.0",
        string content = """{"summary":"Checkout"}""",
        string classification = "Internal",
        string representationKind = "Text") => new
    {
        @ref,
        key = $"k-{Guid.NewGuid():N}"[..16],
        title = "Checkout",
        typeId,
        schemaVersion,
        classification,
        representationKind,
        content,
        reason = "Design coverage",
    };

    private static object Core(
        string idempotencyKey,
        object[] knowledge,
        object[] outline,
        object[]? relations = null,
        object[]? milestones = null,
        object[]? workItems = null,
        bool requireEmptyProject = true) => new
    {
        idempotencyKey,
        requireEmptyProject,
        knowledge,
        outline,
        relations = relations ?? Array.Empty<object>(),
        milestones = milestones ?? Array.Empty<object>(),
        workItems = workItems ?? Array.Empty<object>(),
        workLinks = Array.Empty<object>(),
    };

    private static object MinimalPlan(string idempotencyKey) => new
    {
        idempotencyKey,
        requireEmptyProject = true,
        knowledge = new object[]
        {
            new
            {
                @ref = "root",
                key = $"goal-{Guid.NewGuid():N}"[..20],
                title = "Application goal",
                typeId = "doc.note",
                schemaVersion = "1.0",
                classification = "Internal",
                representationKind = "Text",
                content = "{\"summary\":\"Build the demo\"}",
                reason = "Bootstrap from desktop agent",
            },
            new
            {
                @ref = "req1",
                key = $"req-{Guid.NewGuid():N}"[..20],
                title = "First requirement",
                typeId = "doc.note",
                schemaVersion = "1.0",
                classification = "Internal",
                representationKind = "Text",
                content = "{\"summary\":\"Users can sign in\"}",
                reason = "Captured from existing app",
            },
        },
        outline = new object[]
        {
            new { itemRef = "root", parentRef = (string?)null, position = 0 },
            new { itemRef = "req1", parentRef = "root", position = 0 },
        },
        relations = Array.Empty<object>(),
        milestones = Array.Empty<object>(),
        workItems = new object[]
        {
            new
            {
                @ref = "w1",
                key = $"work-{Guid.NewGuid():N}"[..20],
                title = "Implement sign-in",
                description = "Deliver auth",
                milestoneRef = (string?)null,
                priority = "Normal",
                dependsOnRefs = Array.Empty<string>(),
            },
        },
        workLinks = new object[]
        {
            new { workItemRef = "w1", knowledgeRef = "req1" },
        },
        codebaseTargets = new object[]
        {
            new
            {
                @ref = "repo1",
                provider = "github",
                repositoryId = "https://github.com/acme/checkout.git",
                targetBranch = "main",
                baseCommitSha = "abc1234",
                reason = "Local origin remote",
            },
        },
    };

    private static InitialPlanProposalV1 ToContract(object bundle) =>
        JsonSerializer.Deserialize<InitialPlanProposalV1>(
            JsonSerializer.Serialize(bundle),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    private sealed record Seed(Tenant Tenant, Project Project, Principal Owner, Principal Planner, Principal Executor);
}
