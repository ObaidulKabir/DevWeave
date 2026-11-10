using DevWeave.Domain.Identity;

namespace DevWeave.Domain.Tests.Identity;

/// <summary>
/// Proves TEN-003: human, service-account and AI-agent principals each carry a stable identifier
/// and an explicit principal type.
/// </summary>
public sealed class PrincipalTypeTests
{
    [Fact]
    public void TEN_003_principal_has_a_stable_id_and_its_principal_type()
    {
        var tenant = Tenant.Create("Alpha Corp");

        var principal = Principal.Create(tenant.Id, "agent@alphacorp.com", PrincipalType.AiAgent);

        Assert.NotEqual(Guid.Empty, principal.Id.Value);
        Assert.Equal(tenant.Id, principal.TenantId);
        Assert.Equal(PrincipalType.AiAgent, principal.Type);
        Assert.True(principal.IsActive);
    }

    [Fact]
    public void TEN_003_human_service_account_and_ai_agent_are_distinct_principal_types()
    {
        var tenant = Tenant.Create("Alpha Corp");

        var human = Principal.Create(tenant.Id, "human@alphacorp.com", PrincipalType.Human);
        var serviceAccount = Principal.Create(tenant.Id, "svc@alphacorp.com", PrincipalType.ServiceAccount);
        var agent = Principal.Create(tenant.Id, "agent@alphacorp.com", PrincipalType.AiAgent);

        Assert.Equal(PrincipalType.Human, human.Type);
        Assert.Equal(PrincipalType.ServiceAccount, serviceAccount.Type);
        Assert.Equal(PrincipalType.AiAgent, agent.Type);

        Assert.NotEqual(human.Id, serviceAccount.Id);
        Assert.NotEqual(serviceAccount.Id, agent.Id);
        Assert.NotEqual(human.Id, agent.Id);
    }
}