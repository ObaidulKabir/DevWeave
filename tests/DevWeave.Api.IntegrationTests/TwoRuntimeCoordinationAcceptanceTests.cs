using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevWeave.Contracts.Identity.V1;
using DevWeave.Contracts.Lifecycle.V1;
using DevWeave.Contracts.Planned.V1;
using DevWeave.Contracts.Projects.V1;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;
using DevWeave.Infrastructure.Persistence;
using DevWeave.Mcp.Protocol;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DevWeave.Api.IntegrationTests;

/// <summary>
/// Phase B control-plane readiness gate: two agent runtimes coordinate through DevWeave
/// against one external repository target without persisting source content.
/// </summary>
public sealed class TwoRuntimeCoordinationAcceptanceTests : IClassFixture<IdentityTestFactory>
{
    private static readonly JsonSerializerOptions Json = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };

    private readonly IdentityTestFactory _factory;
    private readonly HttpClient _anon;

    public TwoRuntimeCoordinationAcceptanceTests(IdentityTestFactory factory)
    {
        _factory = factory;
        _factory.PrepareDatabase();
        _anon = _factory.CreateClient();
    }

    [Fact]
    public async Task PhaseB_two_runtimes_coordinate_on_one_repo_via_rest_and_mcp()
    {
        var tenant = await CreateTenantAsync("Coord Agency");
        var owner = await CreateOwnerAsync(tenant.Id, "lead@coord.test");
        var human = _factory.CreateAuthenticatedHumanClient(tenant.Id, owner.Id);

        var profile = await CreateLifecycleProfileAsync(tenant.Id, "coord", "Coord", "1.0");
        var project = await CreateProjectAsync(tenant.Id, "shared-repo", profile.Id, owner.Id);
        var basePath = $"/tenants/{tenant.Id}/projects/{project.Id}/coordination";

        var agentA = await CreateAgentAsync(tenant.Id, "agent-a@coord.test");
        var agentB = await CreateAgentAsync(tenant.Id, "agent-b@coord.test");
        await GrantProjectAccessAsync(tenant.Id, project.Id, agentA.Id, owner.Id);
        await GrantProjectAccessAsync(tenant.Id, project.Id, agentB.Id, owner.Id);
        var clientA = _factory.CreateAuthenticatedAgentClient(tenant.Id, agentA.Id);
        var clientB = _factory.CreateAuthenticatedAgentClient(tenant.Id, agentB.Id);

        // Lock plan baseline + register GitHub codebase target (references only).
        var draft = new BaselineDraft("Ship checkout", Array.Empty<ResourceRef>(), Array.Empty<Guid>(),
            new[] { "PR green", "No source in DevWeave" }, 1);

        // Nothing can be locked until a human-approved plan outline exists.
        var premature = await human.PostAsJsonAsync($"{basePath}/baselines", draft, Json);
        Assert.Equal(HttpStatusCode.Conflict, premature.StatusCode);

        await SeedApprovedPlanAsync(tenant.Id, project.Id, owner.Id);
        var baselineResponse = await human.PostAsJsonAsync($"{basePath}/baselines", draft, Json);
        Assert.Equal(HttpStatusCode.Created, baselineResponse.StatusCode);
        var baselineId = (await baselineResponse.Content.ReadFromJsonAsync<IdResponse>(Json))!.Id;

        var targetResponse = await human.PostAsJsonAsync($"{basePath}/codebase-targets",
            new CodebaseTargetDraft("github", "acme/checkout", "main", "abc1234"), Json);
        Assert.Equal(HttpStatusCode.Created, targetResponse.StatusCode);
        var targetId = (await targetResponse.Content.ReadFromJsonAsync<IdResponse>(Json))!.Id;

        var campaignResponse = await human.PostAsJsonAsync($"{basePath}/campaigns",
            new CampaignDraft(baselineId, new[] { targetId }, "Coordinate checkout work", "Mediated", "camp-1"), Json);
        Assert.Equal(HttpStatusCode.Created, campaignResponse.StatusCode);
        var campaignId = (await campaignResponse.Content.ReadFromJsonAsync<IdResponse>(Json))!.Id;

        var activate = await human.PostAsJsonAsync($"{basePath}/campaigns/{campaignId}/transition",
            new TransitionCommand("Active", "start", 1, "activate-1"), Json);
        Assert.Equal(HttpStatusCode.OK, activate.StatusCode);

        // Register two remote runtimes.
        var runtimeAResponse = await clientA.PostAsJsonAsync($"{basePath}/runtimes",
            new RuntimeRegistration(agentA.Id, "laptop-a", "1.0", new[] { "code.write" }, "attest-a"), Json);
        Assert.Equal(HttpStatusCode.Created, runtimeAResponse.StatusCode);
        var runtimeA = (await runtimeAResponse.Content.ReadFromJsonAsync<IdResponse>(Json))!.Id;

        var runtimeBResponse = await clientB.PostAsJsonAsync($"{basePath}/runtimes",
            new RuntimeRegistration(agentB.Id, "laptop-b", "1.0", new[] { "code.write" }, "attest-b"), Json);
        Assert.Equal(HttpStatusCode.Created, runtimeBResponse.StatusCode);
        var runtimeB = (await runtimeBResponse.Content.ReadFromJsonAsync<IdResponse>(Json))!.Id;

        var packageAResponse = await human.PostAsJsonAsync($"{basePath}/packages",
            new PackageDraft(campaignId, baselineId, "Payments API", Array.Empty<Guid>(), Array.Empty<string>(), Array.Empty<string>()), Json);
        var packageBResponse = await human.PostAsJsonAsync($"{basePath}/packages",
            new PackageDraft(campaignId, baselineId, "UI checkout", Array.Empty<Guid>(), Array.Empty<string>(), Array.Empty<string>()), Json);
        Assert.Equal(HttpStatusCode.Created, packageAResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, packageBResponse.StatusCode);
        var packageA = (await packageAResponse.Content.ReadFromJsonAsync<IdResponse>(Json))!.Id;
        var packageB = (await packageBResponse.Content.ReadFromJsonAsync<IdResponse>(Json))!.Id;

        // Exclusive leases with fencing.
        var leaseAResponse = await clientA.PostAsJsonAsync($"{basePath}/leases",
            new LeaseRequest(packageA, runtimeA, "paths:/payments", DateTimeOffset.UtcNow.AddHours(1), 0, "lease-a"), Json);
        Assert.Equal(HttpStatusCode.Created, leaseAResponse.StatusCode);
        var leaseA = (await leaseAResponse.Content.ReadFromJsonAsync<LeaseView>(Json))!;

        var conflict = await clientB.PostAsJsonAsync($"{basePath}/leases",
            new LeaseRequest(packageA, runtimeB, "paths:/payments", DateTimeOffset.UtcNow.AddHours(1), 0, "lease-a-conflict"), Json);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        var leaseBResponse = await clientB.PostAsJsonAsync($"{basePath}/leases",
            new LeaseRequest(packageB, runtimeB, "paths:/ui", DateTimeOffset.UtcNow.AddHours(1), 0, "lease-b"), Json);
        Assert.Equal(HttpStatusCode.Created, leaseBResponse.StatusCode);

        var renew = await clientA.PostAsJsonAsync($"{basePath}/leases/{leaseA.Id}/renew",
            new { expectedFence = leaseA.Fence, expiresAt = DateTimeOffset.UtcNow.AddHours(2) }, Json);
        Assert.Equal(HttpStatusCode.OK, renew.StatusCode);

        // Channel + MCP durable messaging with reconnect replay.
        var channelResponse = await human.PostAsJsonAsync($"{basePath}/channels",
            new ChannelDraft("Campaign", campaignId, null), Json);
        Assert.Equal(HttpStatusCode.Created, channelResponse.StatusCode);
        var channelId = (await channelResponse.Content.ReadFromJsonAsync<IdResponse>(Json))!.Id;

        clientA.DefaultRequestHeaders.Remove("X-Runtime-Id");
        clientA.DefaultRequestHeaders.Add("X-Runtime-Id", runtimeA.ToString());
        var init = await clientA.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/projects/{project.Id}/mcp/rpc",
            new JsonRpcRequest("2.0", 1, McpMethods.Initialize, null), Json);
        Assert.Equal(HttpStatusCode.OK, init.StatusCode);
        var initRpc = (await init.Content.ReadFromJsonAsync<JsonRpcResponse>(Json))!;
        Assert.Null(initRpc.Error);
        Assert.Contains(McpServer.ProtocolVersion, initRpc.Result!.ToString()!, StringComparison.Ordinal);

        var send = await clientA.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/projects/{project.Id}/mcp/rpc",
            new JsonRpcRequest("2.0", 2, McpMethods.CallTool, new
            {
                name = "send_project_message",
                arguments = new
                {
                    channelId,
                    messageType = "Status",
                    payload = new { note = "payments branch ready" },
                    idempotencyKey = "msg-1"
                }
            }), Json);
        Assert.Equal(HttpStatusCode.OK, send.StatusCode);
        var sendRpc = (await send.Content.ReadFromJsonAsync<JsonRpcResponse>(Json))!;
        Assert.Null(sendRpc.Error);

        clientB.DefaultRequestHeaders.Remove("X-Runtime-Id");
        clientB.DefaultRequestHeaders.Add("X-Runtime-Id", runtimeB.ToString());
        var replay = await clientB.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/projects/{project.Id}/mcp/rpc",
            new JsonRpcRequest("2.0", 3, McpMethods.CallTool, new
            {
                name = "read_messages_after_cursor",
                arguments = new { channelId, afterSequence = 0, limit = 10 }
            }), Json);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayRpc = (await replay.Content.ReadFromJsonAsync<JsonRpcResponse>(Json))!;
        Assert.Null(replayRpc.Error);
        Assert.Contains("payments branch ready", replayRpc.Result!.ToString()!, StringComparison.Ordinal);

        // Change-set refs + integration queue (no source blobs).
        var changeSetResponse = await clientA.PostAsJsonAsync($"{basePath}/change-sets",
            new ChangeSetDraft(packageA, targetId, "github:pr/42", "def5678", new[] { "src/Payments/Api.cs" }), Json);
        Assert.Equal(HttpStatusCode.Created, changeSetResponse.StatusCode);
        var changeSetId = (await changeSetResponse.Content.ReadFromJsonAsync<IdResponse>(Json))!.Id;

        var queueResponse = await human.PostAsJsonAsync($"{basePath}/integration-queue",
            new QueueDraft(changeSetId, "queue-1"), Json);
        Assert.Equal(HttpStatusCode.Created, queueResponse.StatusCode);
        var queueId = (await queueResponse.Content.ReadFromJsonAsync<IdResponse>(Json))!.Id;

        var decide = await human.PostAsJsonAsync($"{basePath}/integration-queue/{queueId}/decide",
            new { expectedFence = 1L }, Json);
        Assert.Equal(HttpStatusCode.OK, decide.StatusCode);

        var workspace = await human.PostAsJsonAsync($"{basePath}/workspace-refs",
            new WorkspaceDraft(packageA, "abc1234"), Json);
        Assert.Equal(HttpStatusCode.OK, workspace.StatusCode);
        var workspaceBody = await workspace.Content.ReadAsStringAsync();
        Assert.Contains("workspace:", workspaceBody, StringComparison.Ordinal);
        Assert.DoesNotContain("class ", workspaceBody, StringComparison.Ordinal);

        // Source-exclusion assertion: no source-bearing payloads in coordination tables.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
        var messages = await db.CoordinationMessages.AsNoTracking().ToListAsync();
        Assert.DoesNotContain(messages, m =>
            m.PayloadJson.Contains("diff --git", StringComparison.OrdinalIgnoreCase) ||
            m.PayloadJson.Contains("```csharp", StringComparison.OrdinalIgnoreCase));
        Assert.All(await db.ChangeSets.AsNoTracking().ToListAsync(), cs =>
        {
            Assert.False(string.IsNullOrWhiteSpace(cs.CommitSha));
            Assert.DoesNotContain("\n+", cs.ProviderRef);
        });
    }

    private sealed record IdResponse(Guid Id);

    private async Task SeedApprovedPlanAsync(Guid tenantId, Guid projectId, Guid actorId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
        var actor = new PrincipalId(actorId);
        var item = KnowledgeItem.Create(new TenantId(tenantId), new ProjectId(projectId), "plan-root",
            "Checkout plan", Classification.Internal, RepresentationKind.Text, "doc.note", "1.0", actor);
        var revision = item.AddRevision("1.0", "{\"summary\":\"Checkout\"}", actor, "Initial plan", null, null, 0);
        item.Approve(revision.Id);
        db.Add(item);
        db.Add(revision);
        db.Add(PlanOutlineNode.Create(item.TenantId, item.ProjectId, item.Id, item.Id, null, 0, actor));
        await db.SaveChangesAsync();
    }

    private async Task<TenantResponse> CreateTenantAsync(string name)
    {
        using var response = await _anon.PostAsJsonAsync("/tenants", new CreateTenantRequest(name), Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TenantResponse>(Json))!;
    }

    private async Task<PrincipalResponse> CreateOwnerAsync(Guid tenantId, string email)
    {
        using var principalResponse = await _anon.PostAsJsonAsync(
            $"/tenants/{tenantId}/principals",
            new CreatePrincipalRequest(email, PrincipalType.Human), Json);
        principalResponse.EnsureSuccessStatusCode();
        var owner = (await principalResponse.Content.ReadFromJsonAsync<PrincipalResponse>(Json))!;

        using var invite = await _anon.PostAsJsonAsync(
            $"/tenants/{tenantId}/members",
            new InviteMemberRequest(owner.Id, MembershipRole.Owner), Json);
        invite.EnsureSuccessStatusCode();
        var membership = (await invite.Content.ReadFromJsonAsync<MembershipResponse>(Json))!;
        using var activate = await _anon.PostAsync($"/tenants/{tenantId}/members/{membership.Id}/activate", null);
        activate.EnsureSuccessStatusCode();
        return owner;
    }

    private async Task<PrincipalResponse> CreateAgentAsync(Guid tenantId, string email)
    {
        using var response = await _anon.PostAsJsonAsync(
            $"/tenants/{tenantId}/principals",
            new CreatePrincipalRequest(email, PrincipalType.AiAgent), Json);
        response.EnsureSuccessStatusCode();
        var agent = (await response.Content.ReadFromJsonAsync<PrincipalResponse>(Json))!;

        using var invite = await _anon.PostAsJsonAsync(
            $"/tenants/{tenantId}/members",
            new InviteMemberRequest(agent.Id, MembershipRole.Member), Json);
        invite.EnsureSuccessStatusCode();
        var membership = (await invite.Content.ReadFromJsonAsync<MembershipResponse>(Json))!;
        using var activate = await _anon.PostAsync($"/tenants/{tenantId}/members/{membership.Id}/activate", null);
        activate.EnsureSuccessStatusCode();
        return agent;
    }

    private async Task GrantProjectAccessAsync(Guid tenantId, Guid projectId, Guid principalId, Guid granterId)
    {
        var client = _factory.CreateAuthenticatedHumanClient(tenantId, granterId);
        using var response = await client.PostAsJsonAsync(
            $"/tenants/{tenantId}/projects/{projectId}/members",
            new GrantProjectMembershipRequest(principalId, ProjectRole.Member), Json);
        response.EnsureSuccessStatusCode();
    }

    private async Task<LifecycleProfileResponse> CreateLifecycleProfileAsync(Guid tenantId, string key, string name, string version)
    {
        using var response = await _anon.PostAsJsonAsync(
            $"/tenants/{tenantId}/lifecycle-profiles",
            new CreateLifecycleProfileRequest(key, name, version), Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LifecycleProfileResponse>(Json))!;
    }

    private async Task<ProjectResponse> CreateProjectAsync(Guid tenantId, string key, Guid lifecycleProfileId, Guid createdBy)
    {
        var client = _factory.CreateAuthenticatedHumanClient(tenantId, createdBy);
        using var response = await client.PostAsJsonAsync(
            $"/tenants/{tenantId}/projects",
            new CreateProjectRequest("Shared Repo", key, null, lifecycleProfileId, createdBy), Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectResponse>(Json))!;
    }
}
