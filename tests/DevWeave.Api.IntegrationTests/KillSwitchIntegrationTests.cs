using DevWeave.Application.Abstractions;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Coordination;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DevWeave.Api.IntegrationTests;

public sealed class KillSwitchIntegrationTests : IClassFixture<IdentityTestFactory>
{
    private readonly IdentityTestFactory _factory;

    public KillSwitchIntegrationTests(IdentityTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MCO_048_kill_switch_works()
    {
        // 1. Setup Tenant and User
        var (client, tenant, user) = await _factory.CreateAuthenticatedHumanClientAsync();
        var project = await _factory.CreateProjectAsync(tenant.Id);
        
        var runtimeId = Guid.NewGuid();
        var scope = new AuthorizedScope(new TenantId(tenant.Id), new PrincipalId(user.Id), new ProjectId(project.Id), "KillSwitch", "trace");

        using var serviceScope = _factory.Services.CreateScope();
        var killSwitchAuthority = serviceScope.ServiceProvider.GetRequiredService<IKillSwitchAuthority>();

        // 2. Verify not killed initially
        var isKilledBefore = await killSwitchAuthority.IsKilledAsync(scope, KillSwitchTargetType.Runtime, runtimeId);
        Assert.False(isKilledBefore);

        // 3. Activate Kill Switch
        await killSwitchAuthority.ActivateAsync(scope, KillSwitchTargetType.Runtime, runtimeId, "Emergency shutdown");

        // 4. Verify killed
        var isKilledAfter = await killSwitchAuthority.IsKilledAsync(scope, KillSwitchTargetType.Runtime, runtimeId);
        Assert.True(isKilledAfter);
    }
}
