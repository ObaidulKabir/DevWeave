using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DevWeave.Contracts.Planned.V1;
using DevWeave.Contracts.Identity.V1;
using DevWeave.Contracts.Projects.V1;
using DevWeave.Application.Abstractions;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Coordination;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DevWeave.Api.IntegrationTests;

public sealed class ParticipationAndQueueIntegrationTests : IClassFixture<IdentityTestFactory>
{
    private readonly IdentityTestFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ParticipationAndQueueIntegrationTests(IdentityTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MCP_001_participation_and_queue_lifecycle_works()
    {
        // 1. Setup Tenant and Agent
        var (client, tenant, user) = await _factory.CreateAuthenticatedHumanClientAsync();
        var project = await _factory.CreateProjectAsync(tenant.Id);
        var agent = await _factory.CreateAgentAsync(tenant.Id, project.Id);
        
        // 2. Register Runtime
        Guid runtimeId;
        using (var scope = _factory.Services.CreateScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<IRuntimeRegistry>();
            runtimeId = await registry.RegisterAsync(
                new AuthorizedScope(new TenantId(tenant.Id), new PrincipalId(agent.Id), new ProjectId(project.Id), "Register", "trace"),
                new RuntimeRegistration(agent.Id, "TestRuntime", "1.0", new List<string> { "code.write" }, "attest-1"));
        }

        // 3. Create Campaign
        Guid campaignId;
        using (var scope = _factory.Services.CreateScope())
        {
            var coordinator = scope.ServiceProvider.GetRequiredService<ICampaignCoordinator>();
            campaignId = await coordinator.CreateAsync(
                new AuthorizedScope(new TenantId(tenant.Id), new PrincipalId(user.Id), new ProjectId(project.Id), "Create", "trace"),
                new CampaignDraft(Guid.NewGuid(), new List<Guid>(), "Fix bugs", "Autonomous", "camp-1"));
        }

        // 4. Participation Offer
        Guid offerId;
        using (var scope = _factory.Services.CreateScope())
        {
            var scheduler = scope.ServiceProvider.GetRequiredService<IParticipationScheduler>();
            offerId = await scheduler.OfferAsync(
                new AuthorizedScope(new TenantId(tenant.Id), new PrincipalId(user.Id), new ProjectId(project.Id), "Offer", "trace"),
                new OfferDraft(campaignId, runtimeId, new List<string> { "code.write" }, DateTimeOffset.UtcNow.AddHours(1)));
        }

        // 5. Decide Offer
        using (var scope = _factory.Services.CreateScope())
        {
            var scheduler = scope.ServiceProvider.GetRequiredService<IParticipationScheduler>();
            var decision = await scheduler.DecideAsync(
                new AuthorizedScope(new TenantId(tenant.Id), new PrincipalId(agent.Id), new ProjectId(project.Id), "Decide", "trace"),
                offerId, true);
            
            Assert.Equal("Accepted", decision.Outcome);
        }

        // 6. Integration Queue
        Guid changeSetId = Guid.NewGuid();
        Guid queueItemId;
        using (var scope = _factory.Services.CreateScope())
        {
            var queue = scope.ServiceProvider.GetRequiredService<IIntegrationQueue>();
            queueItemId = await queue.EnqueueAsync(
                new AuthorizedScope(new TenantId(tenant.Id), new PrincipalId(agent.Id), new ProjectId(project.Id), "Enqueue", "trace"),
                changeSetId, "idempotency-queue-1");
        }

        // 7. Decide Integration
        using (var scope = _factory.Services.CreateScope())
        {
            var queue = scope.ServiceProvider.GetRequiredService<IIntegrationQueue>();
            var decision = await queue.DecideAsync(
                new AuthorizedScope(new TenantId(tenant.Id), new PrincipalId(user.Id), new ProjectId(project.Id), "Integrate", "trace"),
                queueItemId, 12345);
            
            Assert.Equal("Accepted", decision.Outcome);
        }
    }
}
