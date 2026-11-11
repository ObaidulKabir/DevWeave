using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevWeave.Contracts.Planned.V1;
using DevWeave.Contracts.Identity.V1;
using DevWeave.Contracts.Projects.V1;
using DevWeave.Contracts.Lifecycle.V1;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using DevWeave.Mcp.Protocol;
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
        
        var initResult = (await initResponse.Content.ReadFromJsonAsync<JsonRpcResponse>(JsonOptions))!;
        Assert.Null(initResult.Error);
        Assert.NotNull(initResult.Result);

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
    }
}
