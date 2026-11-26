using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevWeave.Contracts.Identity.V1;
using DevWeave.Contracts.Lifecycle.V1;
using DevWeave.Contracts.Planned.V1;
using DevWeave.Contracts.Projects.V1;
using DevWeave.Domain.Agents;
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

        clientA.DefaultRequestHeaders.Remove("X-Runtime-Id");
        clientA.DefaultRequestHeaders.Add("X-Runtime-Id", runtimeA.ToString());
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

    [Fact]
    public async Task BUS_AC_06_one_campaign_coordinates_customer_and_recorded_managed_runtimes()
    {
        var tenant = await CreateTenantAsync($"Hybrid {Guid.NewGuid():N}");
        var owner = await CreateOwnerAsync(tenant.Id, $"lead-{Guid.NewGuid():N}@example.test");
        using var human = _factory.CreateAuthenticatedHumanClient(tenant.Id, owner.Id);
        var profile = await CreateLifecycleProfileAsync(tenant.Id, $"h{Guid.NewGuid():N}"[..12], "Coord", "1");
        var project = await CreateProjectAsync(tenant.Id, $"h{Guid.NewGuid():N}"[..12], profile.Id, owner.Id);
        var basePath = $"/tenants/{tenant.Id}/projects/{project.Id}/coordination";

        var agentA = await CreateAgentAsync(tenant.Id, $"ext-{Guid.NewGuid():N}@example.test");
        var agentB = await CreateAgentAsync(tenant.Id, $"managed-{Guid.NewGuid():N}@example.test");
        await GrantProjectAccessAsync(tenant.Id, project.Id, agentA.Id, owner.Id);
        await GrantProjectAccessAsync(tenant.Id, project.Id, agentB.Id, owner.Id);
        using var clientA = _factory.CreateAuthenticatedAgentClient(tenant.Id, agentA.Id);
        using var clientB = _factory.CreateAuthenticatedAgentClient(tenant.Id, agentB.Id);

        await SeedApprovedPlanAsync(tenant.Id, project.Id, owner.Id);
        var baselineResponse = await human.PostAsJsonAsync($"{basePath}/baselines",
            new BaselineDraft("Hybrid plan", Array.Empty<ResourceRef>(), Array.Empty<Guid>(), new[] { "same policy" }, 1), Json);
        baselineResponse.EnsureSuccessStatusCode();
        var baselineId = (await baselineResponse.Content.ReadFromJsonAsync<IdResponse>(Json))!.Id;

        var campaignResponse = await human.PostAsJsonAsync($"{basePath}/campaigns",
            new CampaignDraft(baselineId, Array.Empty<Guid>(), "Mixed campaign", "Mediated", $"camp-{Guid.NewGuid():N}"), Json);
        campaignResponse.EnsureSuccessStatusCode();
        var campaignId = (await campaignResponse.Content.ReadFromJsonAsync<IdResponse>(Json))!.Id;
        var activate = await human.PostAsJsonAsync($"{basePath}/campaigns/{campaignId}/transition",
            new TransitionCommand("Active", "start", 1, $"activate-{Guid.NewGuid():N}"), Json);
        activate.EnsureSuccessStatusCode();

        var runtimeAResponse = await clientA.PostAsJsonAsync($"{basePath}/runtimes",
            new RuntimeRegistration(agentA.Id, "laptop", "1.0", new[] { "code.write" }, "attest-a", "CustomerManaged", "customer"), Json);
        runtimeAResponse.EnsureSuccessStatusCode();
        var runtimeA = (await runtimeAResponse.Content.ReadFromJsonAsync<IdResponse>(Json))!.Id;
        var runtimeBResponse = await clientB.PostAsJsonAsync($"{basePath}/runtimes",
            new RuntimeRegistration(agentB.Id, "recorded-runner", "1.0", new[] { "code.write" }, "attest-b", "DevWeaveManaged", "devweave", null, "managed.compute"), Json);
        runtimeBResponse.EnsureSuccessStatusCode();
        var runtimeB = (await runtimeBResponse.Content.ReadFromJsonAsync<IdResponse>(Json))!.Id;

        async Task<Guid> AcceptOffer(Guid runtimeId)
        {
            var offerResponse = await human.PostAsJsonAsync($"{basePath}/offers",
                new OfferDraft(campaignId, runtimeId, new[] { "code.write" }, DateTimeOffset.UtcNow.AddHours(1)), Json);
            offerResponse.EnsureSuccessStatusCode();
            var offerId = (await offerResponse.Content.ReadFromJsonAsync<IdResponse>(Json))!.Id;
            var decision = await human.PostAsJsonAsync($"{basePath}/offers/{offerId}/decide", new { accept = true }, Json);
            decision.EnsureSuccessStatusCode();
            var body = await decision.Content.ReadAsStringAsync();
            Assert.Contains("Accepted", body, StringComparison.Ordinal);
            return offerId;
        }

        await AcceptOffer(runtimeA);
        await AcceptOffer(runtimeB);

        var packageAResponse = await human.PostAsJsonAsync($"{basePath}/packages",
            new PackageDraft(campaignId, baselineId, "External slice", Array.Empty<Guid>(), Array.Empty<string>(), Array.Empty<string>(),
                ExecutionLocation: "CustomerManaged", Provider: "customer"), Json);
        var packageBResponse = await human.PostAsJsonAsync($"{basePath}/packages",
            new PackageDraft(campaignId, baselineId, "Recorded managed slice", Array.Empty<Guid>(), Array.Empty<string>(), Array.Empty<string>(),
                ExecutionLocation: "DevWeaveManaged", Provider: "devweave"), Json);
        packageAResponse.EnsureSuccessStatusCode();
        packageBResponse.EnsureSuccessStatusCode();
        var packageA = (await packageAResponse.Content.ReadFromJsonAsync<IdResponse>(Json))!.Id;
        var packageB = (await packageBResponse.Content.ReadFromJsonAsync<IdResponse>(Json))!.Id;

        var listed = await human.GetStringAsync($"{basePath}/campaigns/{campaignId}/packages");
        Assert.Contains(baselineId.ToString(), listed, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CustomerManaged", listed, StringComparison.Ordinal);
        Assert.Contains("DevWeaveManaged", listed, StringComparison.Ordinal);

        var targetResponse = await human.PostAsJsonAsync($"{basePath}/codebase-targets",
            new CodebaseTargetDraft("github", "acme/hybrid", "main", "abc1234"), Json);
        targetResponse.EnsureSuccessStatusCode();
        var targetId = (await targetResponse.Content.ReadFromJsonAsync<IdResponse>(Json))!.Id;

        async Task<Guid> QueueReference(HttpClient client, Guid packageId, Guid runtimeId, string key)
        {
            var leaseResponse = await client.PostAsJsonAsync($"{basePath}/leases",
                new LeaseRequest(packageId, runtimeId, $"paths:/{key}", DateTimeOffset.UtcNow.AddHours(1), 0, $"lease-{key}"), Json);
            leaseResponse.EnsureSuccessStatusCode();
            client.DefaultRequestHeaders.Remove("X-Runtime-Id");
            client.DefaultRequestHeaders.Add("X-Runtime-Id", runtimeId.ToString());
            var changeResponse = await client.PostAsJsonAsync($"{basePath}/change-sets",
                new ChangeSetDraft(packageId, targetId, $"github:pr/{key}", "def5678", new[] { $"{key}/Program.cs" }), Json);
            changeResponse.EnsureSuccessStatusCode();
            var changeSetId = (await changeResponse.Content.ReadFromJsonAsync<IdResponse>(Json))!.Id;
            var queueResponse = await human.PostAsJsonAsync($"{basePath}/integration-queue",
                new QueueDraft(changeSetId, $"queue-{key}"), Json);
            queueResponse.EnsureSuccessStatusCode();
            var queueId = (await queueResponse.Content.ReadFromJsonAsync<IdResponse>(Json))!.Id;
            var stage = await human.PostAsJsonAsync($"{basePath}/integration-queue/{queueId}/stage",
                new { stage = "Reviewed", note = "Same integration policy. DevWeave did not merge." }, Json);
            stage.EnsureSuccessStatusCode();
            return queueId;
        }

        await QueueReference(clientA, packageA, runtimeA, "external");
        await QueueReference(clientB, packageB, runtimeB, "managed");

        var knowledge = await human.GetStringAsync($"/tenants/{tenant.Id}/projects/{project.Id}/knowledge");
        Assert.DoesNotContain("def5678", knowledge, StringComparison.Ordinal);
        Assert.DoesNotContain("managed.compute", knowledge, StringComparison.Ordinal);
    }

    [Fact]
    public async Task COM_023_one_project_cannot_consume_the_other_projects_campaign_slot()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var tenant = await CreateTenantAsync($"Fair {suffix}");
        var owner = await CreateOwnerAsync(tenant.Id, $"lead-{suffix}@example.test");
        using var human = _factory.CreateAuthenticatedHumanClient(tenant.Id, owner.Id);
        using var platform = _factory.CreatePlatformAdminClient(tenant.Id, owner.Id);
        var profile = await CreateLifecycleProfileAsync(tenant.Id, $"f{suffix}"[..12], "Fair", "1");
        var projectA = await CreateProjectAsync(tenant.Id, $"a{suffix}"[..12], profile.Id, owner.Id);
        var projectB = await CreateProjectAsync(tenant.Id, $"b{suffix}"[..12], profile.Id, owner.Id);

        var published = await platform.PostAsJsonAsync($"/tenants/{tenant.Id}/commercial/plans", new PlanDraft(
            $"fair-{suffix}",
            "1.0",
            DateTimeOffset.UtcNow,
            [
                new EntitlementRule("campaigns.concurrent", 2, "count", "Block"),
                new EntitlementRule("campaigns.concurrent.project", 1, "count", "Block"),
            ]), Json);
        published.EnsureSuccessStatusCode();
        var planId = (await published.Content.ReadFromJsonAsync<IdResponse>(Json))!.Id;
        var assigned = await platform.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/commercial/assignments",
            new { planId, effectiveAt = DateTimeOffset.UtcNow }, Json);
        assigned.EnsureSuccessStatusCode();

        await SeedApprovedPlanAsync(tenant.Id, projectA.Id, owner.Id);
        await SeedApprovedPlanAsync(tenant.Id, projectB.Id, owner.Id);
        var baselineA = await LockBaselineAsync(human, tenant.Id, projectA.Id, "Project A plan");
        var baselineB = await LockBaselineAsync(human, tenant.Id, projectB.Id, "Project B plan");

        var first = await human.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/projects/{projectA.Id}/coordination/campaigns",
            new CampaignDraft(baselineA, Array.Empty<Guid>(), "A first", "Mediated", $"a1-{suffix}"), Json);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await human.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/projects/{projectA.Id}/coordination/campaigns",
            new CampaignDraft(baselineA, Array.Empty<Guid>(), "A second", "Mediated", $"a2-{suffix}"), Json);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains("campaigns.concurrent.project", await second.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var other = await human.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/projects/{projectB.Id}/coordination/campaigns",
            new CampaignDraft(baselineB, Array.Empty<Guid>(), "B first", "Mediated", $"b1-{suffix}"), Json);
        Assert.Equal(HttpStatusCode.Created, other.StatusCode);

        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMinutes(-5).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMinutes(5).ToString("O"));
        var decisionsResponse = await human.GetAsync($"/tenants/{tenant.Id}/commercial/decisions?from={from}&to={to}");
        decisionsResponse.EnsureSuccessStatusCode();
        var decisions = await decisionsResponse.Content.ReadAsStringAsync();
        Assert.Contains("campaigns.concurrent.project", decisions, StringComparison.Ordinal);
        Assert.Contains("Blocked", decisions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task COM_022_a_runtime_records_adapter_facts_without_changing_the_campaign()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var tenant = await CreateTenantAsync($"Adapter {suffix}");
        var owner = await CreateOwnerAsync(tenant.Id, $"lead-{suffix}@example.test");
        using var human = _factory.CreateAuthenticatedHumanClient(tenant.Id, owner.Id);
        var profile = await CreateLifecycleProfileAsync(tenant.Id, $"r{suffix}"[..12], "Adapter", "1");
        var project = await CreateProjectAsync(tenant.Id, $"r{suffix}"[..12], profile.Id, owner.Id);
        var basePath = $"/tenants/{tenant.Id}/projects/{project.Id}/coordination";
        var agentA = await CreateAgentAsync(tenant.Id, $"a-{suffix}@example.test");
        var agentB = await CreateAgentAsync(tenant.Id, $"b-{suffix}@example.test");
        await GrantProjectAccessAsync(tenant.Id, project.Id, agentA.Id, owner.Id);
        await GrantProjectAccessAsync(tenant.Id, project.Id, agentB.Id, owner.Id);
        using var clientA = _factory.CreateAuthenticatedAgentClient(tenant.Id, agentA.Id);
        using var clientB = _factory.CreateAuthenticatedAgentClient(tenant.Id, agentB.Id);

        await SeedApprovedPlanAsync(tenant.Id, project.Id, owner.Id);
        var baselineId = await LockBaselineAsync(human, tenant.Id, project.Id, "Adapter plan");
        var campaign = await human.PostAsJsonAsync($"{basePath}/campaigns",
            new CampaignDraft(baselineId, Array.Empty<Guid>(), "Shared campaign", "Mediated", $"camp-{suffix}"), Json);
        campaign.EnsureSuccessStatusCode();

        var registeredA = await clientA.PostAsJsonAsync($"{basePath}/runtimes",
            new RuntimeRegistration(agentA.Id, "laptop", "1.0", new[] { "code.write" }, $"attest-a-{suffix}",
                "CustomerManaged", "customer", "github"), Json);
        registeredA.EnsureSuccessStatusCode();
        var registeredB = await clientB.PostAsJsonAsync($"{basePath}/runtimes",
            new RuntimeRegistration(agentB.Id, "recorded-runner", "1.0", new[] { "code.review" }, $"attest-b-{suffix}",
                "DevWeaveManaged", "devweave", "customer-disk", "managed.compute"), Json);
        registeredB.EnsureSuccessStatusCode();

        var runtimes = await human.GetStringAsync($"{basePath}/runtimes");
        Assert.Contains("code.write", runtimes, StringComparison.Ordinal);
        Assert.Contains("code.review", runtimes, StringComparison.Ordinal);
        Assert.Contains("github", runtimes, StringComparison.Ordinal);
        Assert.Contains("customer-disk", runtimes, StringComparison.Ordinal);
        Assert.Contains("managed.compute", runtimes, StringComparison.Ordinal);
        Assert.Contains("CustomerManaged", runtimes, StringComparison.Ordinal);
        Assert.Contains("DevWeaveManaged", runtimes, StringComparison.Ordinal);

        var campaigns = await human.GetStringAsync($"{basePath}/campaigns");
        Assert.Contains("Shared campaign", campaigns, StringComparison.Ordinal);
        Assert.DoesNotContain("code.write", campaigns, StringComparison.Ordinal);
        Assert.DoesNotContain("code.review", campaigns, StringComparison.Ordinal);
        Assert.DoesNotContain("customer-disk", campaigns, StringComparison.Ordinal);
        Assert.DoesNotContain("managed.compute", campaigns, StringComparison.Ordinal);
    }

    private async Task<Guid> LockBaselineAsync(HttpClient human, Guid tenantId, Guid projectId, string label)
    {
        var response = await human.PostAsJsonAsync(
            $"/tenants/{tenantId}/projects/{projectId}/coordination/baselines",
            new BaselineDraft(label, Array.Empty<ResourceRef>(), Array.Empty<Guid>(), new[] { "same policy" }, 1), Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(Json))!.Id;
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

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
        var tenant = new TenantId(tenantId);
        var project = new ProjectId(projectId);
        var agent = new PrincipalId(principalId);
        var owner = new PrincipalId(granterId);
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
