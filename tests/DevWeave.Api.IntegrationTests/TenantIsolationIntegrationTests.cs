using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevWeave.Application.Abstractions;
using DevWeave.Contracts.Identity.V1;
using DevWeave.Domain.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace DevWeave.Api.IntegrationTests;

/// <summary>
/// Proves the tenant isolation invariants (SRS TEN-001/TEN-002) end-to-end against a real database:
/// a query scoped to tenant A must never disclose tenant B data, across every read path.
/// </summary>
public sealed class TenantIsolationIntegrationTests : IClassFixture<IdentityTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };

    private readonly IdentityTestFactory _factory;
    private readonly HttpClient _client;

    public TenantIsolationIntegrationTests(IdentityTestFactory factory)
    {
        _factory = factory;
        _factory.PrepareDatabase();
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task TEN_001_multiple_tenants_keep_independent_ids_and_settings()
    {
        var alpha = await CreateTenantAsync("Alpha Corp");
        var beta = await CreateTenantAsync("Beta Corp");

        Assert.NotEqual(alpha.Id, beta.Id);

        var fetchedAlpha = await GetTenantAsync(alpha.Id);
        var fetchedBeta = await GetTenantAsync(beta.Id);

        Assert.Equal("Alpha Corp", fetchedAlpha!.Name);
        Assert.Equal("Beta Corp", fetchedBeta!.Name);
    }

    [Fact]
    public async Task TEN_002_cross_tenant_membership_reads_never_disclose_data()
    {
        var alpha = await CreateTenantAsync("Alpha Corp");
        var beta = await CreateTenantAsync("Beta Corp");

        var principalA = await CreatePrincipalAsync(alpha.Id, "a@alphacorp.com", PrincipalType.Human);
        var principalB = await CreatePrincipalAsync(beta.Id, "b@betacorp.com", PrincipalType.Human);

        var membershipA = await InviteMemberAsync(alpha.Id, principalA.Id, MembershipRole.Member);
        var membershipB = await InviteMemberAsync(beta.Id, principalB.Id, MembershipRole.Member);

        // HTTP read path
        var alphaMembers = await ListMembersAsync(alpha.Id);
        Assert.Single(alphaMembers);
        Assert.Equal(membershipA.Id, alphaMembers[0].Id);
        Assert.Equal(alpha.Id, alphaMembers[0].TenantId);
        Assert.DoesNotContain(alphaMembers, m => m.PrincipalId == principalB.Id);

        // Repository read paths (negative disclosure across every read method)
        using var scope = _factory.Services.CreateScope();
        var memberships = scope.ServiceProvider.GetRequiredService<IMembershipRepository>();
        var principals = scope.ServiceProvider.GetRequiredService<IPrincipalRepository>();

        var scopedList = await memberships.ListByTenantAsync(new TenantId(alpha.Id));
        Assert.Single(scopedList);
        Assert.DoesNotContain(scopedList, m => m.Id == new MembershipId(membershipB.Id));

        var crossTenantMembership = await memberships.GetByIdAsync(new TenantId(alpha.Id), new MembershipId(membershipB.Id));
        Assert.Null(crossTenantMembership);

        var crossTenantPrincipal = await principals.GetByIdAsync(new TenantId(alpha.Id), new PrincipalId(principalB.Id));
        Assert.Null(crossTenantPrincipal);
    }

    private async Task<TenantResponse> CreateTenantAsync(string name)
    {
        using var response = await _client.PostAsJsonAsync("/tenants", new CreateTenantRequest(name), JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TenantResponse>(JsonOptions))!;
    }

    private async Task<TenantResponse?> GetTenantAsync(Guid id)
    {
        using var response = await _client.GetAsync($"/tenants/{id}");
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TenantResponse>(JsonOptions);
    }

    private async Task<PrincipalResponse> CreatePrincipalAsync(Guid tenantId, string email, PrincipalType type)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/principals",
            new CreatePrincipalRequest(email, type),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PrincipalResponse>(JsonOptions))!;
    }

    private async Task<MembershipResponse> InviteMemberAsync(Guid tenantId, Guid principalId, MembershipRole role)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/members",
            new InviteMemberRequest(principalId, role),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MembershipResponse>(JsonOptions))!;
    }

    private async Task<List<MembershipResponse>> ListMembersAsync(Guid tenantId)
    {
        using var response = await _client.GetAsync($"/tenants/{tenantId}/members");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<MembershipResponse>>(JsonOptions))!;
    }
}