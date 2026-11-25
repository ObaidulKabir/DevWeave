using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevWeave.Contracts.Identity.V1;
using DevWeave.Contracts.Planned.V1;
using DevWeave.Domain.Identity;
using Xunit;

namespace DevWeave.Api.IntegrationTests;

public sealed class CommercialIntegrationTests : IClassFixture<IdentityTestFactory>
{
    private readonly IdentityTestFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };

    public CommercialIntegrationTests(IdentityTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CommercialPilot_Lifecycle_Success()
    {
        // 1. Setup: Create Tenant and Admin Client
        var (adminClient, tenant, admin) = await _factory.CreateAuthenticatedHumanClientAsync();

        // 2. Publish a Commercial Plan (requires PlatformAdmin)
        var platformAdminClient = _factory.CreatePlatformAdminClient(tenant.Id, admin.Id);
        
        var planDraft = new PlanDraft(
            "standard-2026",
            "1.0",
            DateTimeOffset.UtcNow,
            new List<EntitlementRule>
            {
                new("coordination.sessions", 100, "count", "Block", 0),
                new("storage.bytes", 1024 * 1024 * 100, "bytes", "Warn", 0.02m)
            },
            Price: 149,
            Currency: "USD");

        var publishResponse = await platformAdminClient.PostAsJsonAsync($"/tenants/{tenant.Id}/commercial/plans", planDraft, _jsonOptions);
        publishResponse.EnsureSuccessStatusCode();
        var plan = (await publishResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions))!;
        Guid planId = plan.GetProperty("id").GetGuid();

        // 3. Assign Plan to Tenant
        var assignResponse = await platformAdminClient.PostAsJsonAsync($"/tenants/{tenant.Id}/commercial/assignments", new { planId, effectiveAt = DateTimeOffset.UtcNow }, _jsonOptions);
        assignResponse.EnsureSuccessStatusCode();

        // 4. Record Usage (requires Agent token)
        var agent = await _factory.CreateAgentAsync(tenant.Id, Guid.NewGuid());
        var agentClient = _factory.CreateAuthenticatedAgentClient(tenant.Id, agent.Id);

        var projectId = Guid.NewGuid();
        var usageDraft = new UsageObservation(
            projectId, null, null,
            "CustomerManaged", "Standard", "coordination.sessions",
            1, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString());

        var usageResponse = await agentClient.PostAsJsonAsync($"/tenants/{tenant.Id}/commercial/usage", usageDraft, _jsonOptions);
        usageResponse.EnsureSuccessStatusCode();

        // 5. Evaluate Entitlement
        var evalResponse = await agentClient.PostAsJsonAsync($"/tenants/{tenant.Id}/commercial/evaluate", new { entitlementKey = "coordination.sessions", requested = 1 }, _jsonOptions);
        evalResponse.EnsureSuccessStatusCode();
        var decision = (await evalResponse.Content.ReadFromJsonAsync<Decision>(_jsonOptions))!;
        
        Assert.Equal("Allowed", decision.Outcome);

        var from = DateTimeOffset.UtcNow.AddMinutes(-5);
        var to = DateTimeOffset.UtcNow.AddMinutes(5);
        var decisionsResponse = await adminClient.GetAsync(
            $"/tenants/{tenant.Id}/commercial/decisions?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}");
        decisionsResponse.EnsureSuccessStatusCode();
        var decisions = (await decisionsResponse.Content.ReadFromJsonAsync<List<EntitlementDecisionView>>(_jsonOptions))!;
        Assert.Contains(decisions, row => row.Meter == "coordination.sessions" && row.Outcome == "Allowed" && row.PlanVersion == "1.0");

        var trainingResponse = await adminClient.GetAsync($"/tenants/{tenant.Id}/commercial/training");
        trainingResponse.EnsureSuccessStatusCode();
        var training = (await trainingResponse.Content.ReadFromJsonAsync<TrainingUseView>(_jsonOptions))!;
        Assert.Equal("Excluded", training.Use);

        var optIn = await adminClient.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/commercial/training/opt-in",
            new TrainingOptInRequest("customer contract 14"),
            _jsonOptions);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, optIn.StatusCode);

        var opted = (await (await adminClient.GetAsync($"/tenants/{tenant.Id}/commercial/training"))
            .Content.ReadFromJsonAsync<TrainingUseView>(_jsonOptions))!;
        Assert.Equal("OptedIn", opted.Use);
        Assert.Equal("customer contract 14", opted.Reason);

        var usageList = await adminClient.GetAsync(
            $"/tenants/{tenant.Id}/commercial/usage?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}");
        usageList.EnsureSuccessStatusCode();
        var usage = (await usageList.Content.ReadFromJsonAsync<List<UsageView>>(_jsonOptions))!;
        Assert.Contains(usage, row =>
            row.Meter == "coordination.sessions" &&
            row.ProjectId == projectId &&
            row.ExecutionMode == "CustomerManaged");

        var planResponse = await adminClient.GetAsync($"/tenants/{tenant.Id}/commercial/plan");
        planResponse.EnsureSuccessStatusCode();
        var active = (await planResponse.Content.ReadFromJsonAsync<ActivePlanView>(_jsonOptions))!;
        Assert.Equal("standard-2026", active.Key);
        Assert.Equal("1.0", active.Version);
        Assert.Contains(active.Entitlements, rule => rule.Key == "coordination.sessions" && rule.Action == "Block");
        Assert.Contains(active.Entitlements, rule => rule.Key == "storage.bytes" && rule.Action == "Warn");
    }
}
