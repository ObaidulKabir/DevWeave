using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevWeave.Contracts.Planned.V1;
using DevWeave.Contracts.Identity.V1;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DevWeave.Application.Abstractions;
using DevWeave.Infrastructure.Persistence;

namespace DevWeave.Api.IntegrationTests.Integrations;

public sealed class IntegrationIntegrationTests : IClassFixture<IdentityTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };

    private readonly IdentityTestFactory _factory;
    private readonly HttpClient _client;

    public IntegrationIntegrationTests(IdentityTestFactory factory)
    {
        _factory = factory;
        _factory.PrepareDatabase();
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task INT_001_004_integration_lifecycle_and_event_ingress_works_end_to_end()
    {
        // 1. Setup Tenant and Owner
        var tenant = await CreateTenantAsync("Alpha Corp");
        var owner = await CreateOwnerAsync(tenant.Id, "owner@alphacorp.com");
        var client = _factory.CreateAuthenticatedHumanClient(tenant.Id, owner.Id);

        // 2. Connect Integration (INT-001)
        var draft = new IntegrationDraft("GitHub", "repo,admin:org", "secret-ref-1", new[] { "repository", "sender" });
        var connectResponse = await client.PostAsJsonAsync($"/tenants/{tenant.Id}/integrations", draft, JsonOptions);
        Assert.Equal(HttpStatusCode.Accepted, connectResponse.StatusCode);

        var connectResult = await connectResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var integrationId = connectResult.GetProperty("id").GetGuid();

        // 3. Verify Integration in DB
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
            var integration = await context.Integrations.FindAsync(new IntegrationId(integrationId));
            Assert.NotNull(integration);
            Assert.Equal("GitHub", integration!.Provider);
            Assert.Equal(IntegrationStatus.Connected, integration.Status);
        }

        // 4. Accept Inbound Event (INT-004)
        var envelope = new IngressEnvelope(
            "evt_123",
            DateTimeOffset.UtcNow,
            "1.0",
            "{\"repository\": \"owner/repo\", \"sender\": \"user1\"}");

        var eventResponse = await client.PostAsJsonAsync($"/tenants/{tenant.Id}/integrations/{integrationId}/events", envelope, JsonOptions);
        Assert.Equal(HttpStatusCode.Accepted, eventResponse.StatusCode);

        var eventResult = await eventResponse.Content.ReadFromJsonAsync<Decision>(JsonOptions);
        Assert.Equal("Accepted", eventResult!.Outcome);

        // 5. Verify Event and Outbox record
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DevWeaveDbContext>();

            var inboundEvent = await context.InboundEvents
                .FirstOrDefaultAsync(e => e.IntegrationId == new IntegrationId(integrationId) && e.ProviderEventId == "evt_123");
            Assert.NotNull(inboundEvent);
            Assert.NotNull(inboundEvent!.ProcessedAt);

            var outboxMessage = await context.OutboxMessages
                .FirstOrDefaultAsync(m => m.TenantId == new TenantId(tenant.Id) && m.EventType == "IntegrationEvent:GitHub");
            Assert.NotNull(outboxMessage);
            
            var expectedJson = JsonDocument.Parse(envelope.MetadataJson);
             var actualJson = JsonDocument.Parse(outboxMessage!.PayloadJson);
             Assert.Equal(expectedJson.RootElement.GetProperty("repository").GetString(), actualJson.RootElement.GetProperty("repository").GetString());
             Assert.Equal(expectedJson.RootElement.GetProperty("sender").GetString(), actualJson.RootElement.GetProperty("sender").GetString());
         }

        // 6. Test Deduplication (Conflict)
        var duplicateResponse = await client.PostAsJsonAsync($"/tenants/{tenant.Id}/integrations/{integrationId}/events", envelope, JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        // 7. Rotate Credential
        var rotateResponse = await client.PostAsJsonAsync($"/tenants/{tenant.Id}/integrations/{integrationId}/rotate", "new-secret-ref", JsonOptions);
        Assert.Equal(HttpStatusCode.NoContent, rotateResponse.StatusCode);

        // 8. Disconnect
        var disconnectResponse = await client.DeleteAsync($"/tenants/{tenant.Id}/integrations/{integrationId}");
        Assert.Equal(HttpStatusCode.NoContent, disconnectResponse.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
            var integration = await context.Integrations.FindAsync(new IntegrationId(integrationId));
            Assert.Equal(IntegrationStatus.Disconnected, integration!.Status);
            Assert.Equal("new-secret-ref", integration.CredentialRef);
        }
    }

    private async Task<TenantResponse> CreateTenantAsync(string name)
    {
        using var response = await _client.PostAsJsonAsync("/tenants", new CreateTenantRequest(name), JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TenantResponse>(JsonOptions))!;
    }

    private async Task<PrincipalResponse> CreateOwnerAsync(Guid tenantId, string email)
    {
        using var principalResponse = await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/principals",
            new CreatePrincipalRequest(email, PrincipalType.Human),
            JsonOptions);
        principalResponse.EnsureSuccessStatusCode();
        var owner = (await principalResponse.Content.ReadFromJsonAsync<PrincipalResponse>(JsonOptions))!;

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
}
