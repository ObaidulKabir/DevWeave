using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevWeave.Contracts.Planned.V1;
using DevWeave.Contracts.Identity.V1;
using DevWeave.Contracts.Projects.V1;
using DevWeave.Contracts.Lifecycle.V1;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;
using DevWeave.Infrastructure.Persistence;
using DevWeave.Mcp.Protocol;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DevWeave.Api.IntegrationTests;

public sealed class McpCoordinationIntegrationTests : IClassFixture<IdentityTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };

    private readonly IdentityTestFactory _factory;
    private readonly HttpClient _client;

    public McpCoordinationIntegrationTests(IdentityTestFactory factory)
    {
        _factory = factory;
        _factory.PrepareDatabase();
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task MCP_033_initialize_accepts_only_the_advertised_protocol_version()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var tenant = await CreateTenantAsync($"Protocol {suffix}");
        var owner = await CreateOwnerAsync(tenant.Id, $"owner-{suffix}@example.test");
        var agent = await CreateAgentAsync(tenant.Id, $"agent-{suffix}@example.test");
        var profile = await CreateLifecycleProfileAsync(tenant.Id, $"p{suffix}"[..12], "Protocol", "1");
        var project = await CreateProjectAsync(tenant.Id, $"p{suffix}"[..12], profile.Id, owner.Id);
        await GrantProjectAccessAsync(tenant.Id, project.Id, agent.Id, owner.Id);
        using var client = _factory.CreateAuthenticatedAgentClient(tenant.Id, agent.Id);
        var url = $"/tenants/{tenant.Id}/projects/{project.Id}/mcp/rpc";

        using var accepted = await client.PostAsJsonAsync(url,
            new JsonRpcRequest("2.0", 1, McpMethods.Initialize, new { protocolVersion = McpServer.ProtocolVersion }),
            JsonOptions);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using var acceptedDoc = JsonDocument.Parse(await accepted.Content.ReadAsStringAsync());
        Assert.False(acceptedDoc.RootElement.TryGetProperty("error", out var acceptedError) &&
                     acceptedError.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined);
        var result = acceptedDoc.RootElement.GetProperty("result");
        Assert.Equal(McpServer.ProtocolVersion, result.GetProperty("protocolVersion").GetString());
        Assert.Equal(McpServer.ProtocolVersion, result.GetProperty("supportedProtocolVersions")[0].GetString());

        using var refused = await client.PostAsJsonAsync(url,
            new JsonRpcRequest("2.0", 2, McpMethods.Initialize, new { protocolVersion = "1999-01-01" }),
            JsonOptions);
        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        using var refusedDoc = JsonDocument.Parse(await refused.Content.ReadAsStringAsync());
        var error = refusedDoc.RootElement.GetProperty("error");
        Assert.Equal(-32602, error.GetProperty("code").GetInt32());
        Assert.Equal("invalid", error.GetProperty("data").GetProperty("kind").GetString());
        var message = error.GetProperty("message").GetString();
        Assert.Contains("1999-01-01", message, StringComparison.Ordinal);
        Assert.Contains(McpServer.ProtocolVersion, message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MCP_001_035_agent_coordination_lifecycle_works()
    {
        // 1. Setup Tenant and Agent
        var tenant = await CreateTenantAsync("MCP Corp");
        var agent = await CreateAgentAsync(tenant.Id, "coord-agent@mcp.test");
        var client = _factory.CreateAuthenticatedAgentClient(tenant.Id, agent.Id);

        // 2. Setup Project
        var owner = await CreateOwnerAsync(tenant.Id, "owner@mcp.test");
        var ownerClient = _factory.CreateAuthenticatedHumanClient(tenant.Id, owner.Id);
        var profile = await CreateLifecycleProfileAsync(tenant.Id, "mcp-lifecycle", "MCP Lifecycle", "1.0");
        var project = await CreateProjectAsync(tenant.Id, "mcp-project", profile.Id, owner.Id);

        // Grant Agent access to Project (for RLS and middleware to pass)
        await GrantProjectAccessAsync(tenant.Id, project.Id, agent.Id, owner.Id);

        // 3. MCP Initialize
        var initRequest = new JsonRpcRequest("2.0", 1, McpMethods.Initialize, null);
        var initResponse = await client.PostAsJsonAsync($"/tenants/{tenant.Id}/projects/{project.Id}/mcp/rpc", initRequest, JsonOptions);
        Assert.Equal(HttpStatusCode.OK, initResponse.StatusCode);
        
        var initPayload = await initResponse.Content.ReadAsStringAsync();
        using var initEnvelope = JsonDocument.Parse(initPayload);
        Assert.False(initEnvelope.RootElement.TryGetProperty("error", out var initError) &&
                     initError.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined);
        var initResult = initEnvelope.RootElement.GetProperty("result");
        Assert.Equal("1.4.0", initResult.GetProperty("serverInfo").GetProperty("version").GetString());
        var instructions = initResult.GetProperty("instructions").GetString();
        Assert.Contains("classification ceiling", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("checksum", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OIDC", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TEN-009", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("propose_initial_plan", instructions, StringComparison.Ordinal);
        Assert.Contains("list_schemas", instructions, StringComparison.Ordinal);
        Assert.Contains("Plan import", instructions, StringComparison.Ordinal);
        Assert.Contains("send_project_message", instructions, StringComparison.Ordinal);
        Assert.Contains("codebaseTargets", instructions, StringComparison.Ordinal);
        Assert.Contains("register_codebase_target", instructions, StringComparison.Ordinal);
        Assert.Contains("report_work_item", instructions, StringComparison.Ordinal);
        Assert.Contains("list_work_items", instructions, StringComparison.Ordinal);

        // 4. Register Runtime
        var registration = new RuntimeRegistration(agent.Id, "DotNetRuntime", "1.0", new List<string>(), "session-1");
        
        // For the test, I'll assume we have a runtimeId. 
        // I'll use the CoordinationService to register it manually in the test setup.
        Guid runtimeId;
        using (var scope = _factory.Services.CreateScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<IRuntimeRegistry>();
            runtimeId = await registry.RegisterAsync(
                new AuthorizedScope(new Domain.Identity.TenantId(tenant.Id), new Domain.Identity.PrincipalId(agent.Id), new Domain.Projects.ProjectId(project.Id), "Register", "trace"),
                registration);
        }

        // 5. Open Channel
        Guid channelId;
        using (var scope = _factory.Services.CreateScope())
        {
            var channels = scope.ServiceProvider.GetRequiredService<ICoordinationChannels>();
            channelId = await channels.OpenAsync(
                new AuthorizedScope(new Domain.Identity.TenantId(tenant.Id), new Domain.Identity.PrincipalId(agent.Id), new Domain.Projects.ProjectId(project.Id), "Open", "trace"),
                "Direct", null, null);
        }

        // 6. Send Message via MCP RPC
        var draft = new MessageDraft(channelId, "TaskStarted", "1.0", "{\"task\":\"T1\"}", new List<ResourceRef>(), null, "idempotency-1");
        var sendRequest = new JsonRpcRequest("2.0", 3, McpMethods.SendMessage, draft);
        client.DefaultRequestHeaders.Add("X-Runtime-Id", runtimeId.ToString());
        
        var sendResponse = await client.PostAsJsonAsync($"/tenants/{tenant.Id}/projects/{project.Id}/mcp/rpc", sendRequest, JsonOptions);
        Assert.Equal(HttpStatusCode.OK, sendResponse.StatusCode);
        
        var sendResult = (await sendResponse.Content.ReadFromJsonAsync<JsonRpcResponse>(JsonOptions))!;
        Assert.Null(sendResult.Error);
        
        var message = JsonSerializer.Deserialize<MessageView>(sendResult.Result!.ToString()!, JsonOptions);
        Assert.Equal(1, message!.Sequence);
        Assert.Equal("TaskStarted", message.MessageType);

        var deliveryUrl = $"/tenants/{tenant.Id}/projects/{project.Id}/coordination/channels/{channelId}/delivery?through=1";
        var persisted = await client.GetFromJsonAsync<MessageDeliveryView>(deliveryUrl, JsonOptions);
        Assert.True(persisted!.Persisted);
        Assert.False(persisted.Notified);
        Assert.False(persisted.Read);
        Assert.False(persisted.Acknowledged);
        Assert.False(persisted.Accepted);

        // 7. Verify Replay
        using (var scope = _factory.Services.CreateScope())
        {
            var channels = scope.ServiceProvider.GetRequiredService<ICoordinationChannels>();
            var replay = await channels.ReplayAsync(
                new AuthorizedScope(new Domain.Identity.TenantId(tenant.Id), new Domain.Identity.PrincipalId(agent.Id), new Domain.Projects.ProjectId(project.Id), "Replay", "trace"),
                channelId, 0, 10);
            
            Assert.Single(replay.Items);
            Assert.Equal("TaskStarted", replay.Items[0].MessageType);
        }

        var read = await client.GetFromJsonAsync<MessageDeliveryView>(deliveryUrl, JsonOptions);
        Assert.True(read!.Read);
        Assert.False(read.Acknowledged);
        Assert.False(read.Accepted);

        var mark = new CursorDraft(channelId, 1);
        using (var notify = await client.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/projects/{project.Id}/coordination/channels/{channelId}/notify", mark, JsonOptions))
            notify.EnsureSuccessStatusCode();
        var notified = await client.GetFromJsonAsync<MessageDeliveryView>(deliveryUrl, JsonOptions);
        Assert.True(notified!.Notified);
        Assert.False(notified.Accepted);

        using (var ack = await client.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/projects/{project.Id}/coordination/channels/{channelId}/ack", mark, JsonOptions))
            ack.EnsureSuccessStatusCode();
        var acknowledged = await client.GetFromJsonAsync<MessageDeliveryView>(deliveryUrl, JsonOptions);
        Assert.True(acknowledged!.Acknowledged);
        Assert.False(acknowledged.Accepted);

        using (var accept = await client.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/projects/{project.Id}/coordination/channels/{channelId}/accept", mark, JsonOptions))
            accept.EnsureSuccessStatusCode();
        var accepted = await client.GetFromJsonAsync<MessageDeliveryView>(deliveryUrl, JsonOptions);
        Assert.True(accepted!.Accepted);
        Assert.True(accepted.Persisted);

        var deliveryCall = new JsonRpcRequest("2.0", 4, McpMethods.CallTool, new
        {
            name = "get_message_delivery",
            arguments = new { channelId, through = 1 },
        });
        var deliveryResponse = await client.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/projects/{project.Id}/mcp/rpc", deliveryCall, JsonOptions);
        deliveryResponse.EnsureSuccessStatusCode();
        var deliveryResult = (await deliveryResponse.Content.ReadFromJsonAsync<JsonRpcResponse>(JsonOptions))!;
        Assert.Null(deliveryResult.Error);
        var toolDelivery = JsonSerializer.Deserialize<MessageDeliveryView>(deliveryResult.Result!.ToString()!, JsonOptions);
        Assert.True(toolDelivery!.Persisted);
        Assert.True(toolDelivery.Notified);
        Assert.True(toolDelivery.Read);
        Assert.True(toolDelivery.Acknowledged);
        Assert.True(toolDelivery.Accepted);
    }

    [Fact]
    public async Task BUS_AC_02_an_external_agent_connects_without_purchasing_managed_compute()
    {
        var tenant = await CreateTenantAsync($"External {Guid.NewGuid():N}");
        var agent = await CreateAgentAsync(tenant.Id, $"ext-{Guid.NewGuid():N}@example.test");
        var owner = await CreateOwnerAsync(tenant.Id, $"owner-{Guid.NewGuid():N}@example.test");
        var profile = await CreateLifecycleProfileAsync(tenant.Id, $"p{Guid.NewGuid():N}"[..12], "Plan", "1");
        var project = await CreateProjectAsync(tenant.Id, $"e{Guid.NewGuid():N}"[..12], profile.Id, owner.Id);
        await GrantProjectAccessAsync(tenant.Id, project.Id, agent.Id, owner.Id);

        using var ownerHttp = _factory.CreateAuthenticatedHumanClient(tenant.Id, owner.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await ownerHttp.GetAsync($"/tenants/{tenant.Id}/commercial/plan")).StatusCode);

        using var agentHttp = _factory.CreateAuthenticatedAgentClient(tenant.Id, agent.Id);
        var rpc = $"/tenants/{tenant.Id}/projects/{project.Id}/mcp/rpc";
        var external = new JsonRpcRequest("2.0", 1, McpMethods.CallTool, new
        {
            name = "register_runtime",
            arguments = new
            {
                runtimeClass = "cursor",
                protocolVersion = "1.0",
                capabilities = Array.Empty<string>(),
                attestationRef = "laptop-1",
            },
        });
        var externalResponse = await agentHttp.PostAsJsonAsync(rpc, external, JsonOptions);
        externalResponse.EnsureSuccessStatusCode();
        var externalBody = (await externalResponse.Content.ReadFromJsonAsync<JsonRpcResponse>(JsonOptions))!;
        Assert.Null(externalBody.Error);
        using var externalResult = JsonDocument.Parse(externalBody.Result!.ToString()!);
        var externalRuntimeId = externalResult.RootElement.GetProperty("id").GetGuid();

        var managed = new JsonRpcRequest("2.0", 2, McpMethods.CallTool, new
        {
            name = "register_runtime",
            arguments = new
            {
                runtimeClass = "runner",
                protocolVersion = "1.0",
                capabilities = Array.Empty<string>(),
                attestationRef = "runner-1",
                executionLocation = "DevWeaveManaged",
                provider = "devweave",
                billingMeter = "managed.compute",
            },
        });
        var managedResponse = await agentHttp.PostAsJsonAsync(rpc, managed, JsonOptions);
        managedResponse.EnsureSuccessStatusCode();
        Assert.Null((await managedResponse.Content.ReadFromJsonAsync<JsonRpcResponse>(JsonOptions))!.Error);

        var runtimes = await ownerHttp.GetStringAsync($"/tenants/{tenant.Id}/projects/{project.Id}/coordination/runtimes");
        Assert.Contains("CustomerManaged", runtimes, StringComparison.Ordinal);
        Assert.Contains("DevWeaveManaged", runtimes, StringComparison.Ordinal);
        Assert.Contains("managed.compute", runtimes, StringComparison.Ordinal);

        var knowledge = await ownerHttp.GetStringAsync($"/tenants/{tenant.Id}/projects/{project.Id}/knowledge");
        Assert.DoesNotContain("laptop-1", knowledge, StringComparison.Ordinal);
        Assert.DoesNotContain("managed.compute", knowledge, StringComparison.Ordinal);

        var started = DateTimeOffset.UtcNow.AddMinutes(-1);
        var ended = DateTimeOffset.UtcNow;
        using var customerUsage = await agentHttp.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/commercial/usage",
            new UsageObservation(project.Id, null, externalRuntimeId, "CustomerManaged", "session", "platform.session", 1, started, ended, Guid.NewGuid().ToString()),
            JsonOptions);
        customerUsage.EnsureSuccessStatusCode();
        using var managedUsage = await agentHttp.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/commercial/usage",
            new UsageObservation(project.Id, null, null, "DevWeaveManaged", "compute", "managed.compute", 2, started, ended, Guid.NewGuid().ToString()),
            JsonOptions);
        managedUsage.EnsureSuccessStatusCode();

        var from = Uri.EscapeDataString(started.AddMinutes(-1).ToString("O"));
        var to = Uri.EscapeDataString(ended.AddMinutes(1).ToString("O"));
        var usage = await ownerHttp.GetStringAsync($"/tenants/{tenant.Id}/commercial/usage?from={from}&to={to}");
        Assert.Contains("platform.session", usage, StringComparison.Ordinal);
        Assert.Contains("CustomerManaged", usage, StringComparison.Ordinal);
        Assert.Contains("managed.compute", usage, StringComparison.Ordinal);
        Assert.Contains("DevWeaveManaged", usage, StringComparison.Ordinal);
    }

    private async Task<TenantResponse> CreateTenantAsync(string name)
    {
        using var response = await _client.PostAsJsonAsync("/tenants", new CreateTenantRequest(name), JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TenantResponse>(JsonOptions))!;
    }

    private async Task<PrincipalResponse> CreateAgentAsync(Guid tenantId, string email)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/principals",
            new CreatePrincipalRequest(email, PrincipalType.AiAgent),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        var agent = (await response.Content.ReadFromJsonAsync<PrincipalResponse>(JsonOptions))!;

        using var invite = await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/members",
            new InviteMemberRequest(agent.Id, MembershipRole.Member),
            JsonOptions);
        invite.EnsureSuccessStatusCode();
        var membership = (await invite.Content.ReadFromJsonAsync<MembershipResponse>(JsonOptions))!;

        using var activate = await _client.PostAsync(
            $"/tenants/{tenantId}/members/{membership.Id}/activate",
            content: null);
        activate.EnsureSuccessStatusCode();

        return agent;
    }

    private async Task<PrincipalResponse> CreateOwnerAsync(Guid tenantId, string email)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/principals",
            new CreatePrincipalRequest(email, PrincipalType.Human),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        var owner = (await response.Content.ReadFromJsonAsync<PrincipalResponse>(JsonOptions))!;

        using var invite = await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/members",
            new InviteMemberRequest(owner.Id, MembershipRole.Owner),
            JsonOptions);
        invite.EnsureSuccessStatusCode();
        var membership = (await invite.Content.ReadFromJsonAsync<MembershipResponse>(JsonOptions))!;

        using var activate = await _client.PostAsync(
            $"/tenants/{tenantId}/members/{membership.Id}/activate",
            content: null);
        activate.EnsureSuccessStatusCode();

        return owner;
    }

    private async Task<LifecycleProfileResponse> CreateLifecycleProfileAsync(Guid tenantId, string key, string name, string version)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/lifecycle-profiles",
            new CreateLifecycleProfileRequest(key, name, version),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LifecycleProfileResponse>(JsonOptions))!;
    }

    private async Task<ProjectResponse> CreateProjectAsync(Guid tenantId, string key, Guid lifecycleProfileId, Guid createdBy)
    {
        var client = _factory.CreateAuthenticatedHumanClient(tenantId, createdBy);
        using var response = await client.PostAsJsonAsync(
            $"/tenants/{tenantId}/projects",
            new CreateProjectRequest("MCP Project", key, null, lifecycleProfileId, createdBy),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions))!;
    }

    private async Task GrantProjectAccessAsync(Guid tenantId, Guid projectId, Guid principalId, Guid ownerId)
    {
        var client = _factory.CreateAuthenticatedHumanClient(tenantId, ownerId);
        using var response = await client.PostAsJsonAsync(
            $"/tenants/{tenantId}/projects/{projectId}/members",
            new GrantProjectMembershipRequest(principalId, ProjectRole.Member),
            JsonOptions);
        response.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
        var tenant = new TenantId(tenantId);
        var project = new ProjectId(projectId);
        var agent = new PrincipalId(principalId);
        var owner = new PrincipalId(ownerId);
        foreach (var capability in new[]
                 {
                     AgentCapability.ReadKnowledge, AgentCapability.WriteKnowledge, AgentCapability.ExecuteWorkflow,
                 })
        {
            var grant = AgentGrant.Propose(tenant, project, agent, capability, null, Classification.Internal);
            grant.Approve(owner);
            db.Add(grant);
        }
        if (!await db.AgentCredentials.AnyAsync(c => c.TenantId == tenant && c.AgentId == agent))
            db.Add(AgentCredential.Issue(tenant, agent, "test", "hash", null));
        await db.SaveChangesAsync();
    }
}
