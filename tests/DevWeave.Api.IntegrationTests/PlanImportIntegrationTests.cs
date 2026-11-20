using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DevWeave.Application.Abstractions;
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
        Assert.DoesNotContain("request_assignment", plannerTools);

        using var listExecutor = await executor.PostAsJsonAsync(url, new JsonRpcRequest("2.0", 4, McpMethods.ListTools, new { }));
        using var listExecutorDoc = await listExecutor.Content.ReadFromJsonAsync<JsonDocument>();
        var executorTools = listExecutorDoc!.RootElement.GetProperty("result").GetProperty("tools")
            .EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToHashSet();
        Assert.Contains("request_assignment", executorTools);
        Assert.DoesNotContain("propose_initial_plan", executorTools);
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
    };

    private static InitialPlanProposalV1 ToContract(object bundle) =>
        JsonSerializer.Deserialize<InitialPlanProposalV1>(
            JsonSerializer.Serialize(bundle),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    private sealed record Seed(Tenant Tenant, Project Project, Principal Owner, Principal Planner, Principal Executor);
}
