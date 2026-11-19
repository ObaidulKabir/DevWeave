using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace DevWeave.Api.IntegrationTests;

public sealed class TenantRegistrationIntegrationTests : IClassFixture<IdentityTestFactory>
{
    private readonly IdentityTestFactory _factory;

    public TenantRegistrationIntegrationTests(IdentityTestFactory factory)
    {
        _factory = factory;
        _factory.PrepareDatabase();
    }

    [Fact]
    public async Task Register_creates_an_active_owner_and_a_development_session_without_email_verification()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/auth/register",
            new { tenantName = "Acme Delivery", ownerEmail = "owner@acme.example" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var tenantId = body.RootElement.GetProperty("tenantId").GetGuid();
        var principalId = body.RootElement.GetProperty("principalId").GetGuid();
        Assert.NotEqual(Guid.Empty, tenantId);
        Assert.NotEqual(Guid.Empty, principalId);
        Assert.Equal("owner@acme.example", body.RootElement.GetProperty("email").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("accessToken").GetString()));
        Assert.Equal("Bearer", body.RootElement.GetProperty("tokenType").GetString());
        Assert.False(body.RootElement.TryGetProperty("verificationRequired", out _));

        using var membersRequest = new HttpRequestMessage(HttpMethod.Get, $"/tenants/{tenantId}/members");
        membersRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            IdentityTestFactory.IssueTestOidcToken(tenantId, principalId));
        using var membersResponse = await client.SendAsync(membersRequest);

        Assert.Equal(HttpStatusCode.OK, membersResponse.StatusCode);
        using var members = JsonDocument.Parse(await membersResponse.Content.ReadAsStringAsync());
        var membership = Assert.Single(members.RootElement.EnumerateArray());
        Assert.Equal(principalId, membership.GetProperty("principalId").GetGuid());
        Assert.Equal("Owner", membership.GetProperty("role").GetString());
        Assert.Equal("Active", membership.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Register_rejects_a_missing_owner_email()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/auth/register",
            new { tenantName = "Acme Delivery", ownerEmail = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_with_oidc_configured_still_creates_the_tenant_and_omits_a_local_token()
    {
        using var oidcFactory = new IdentityTestFactory(enforceTenantMembership: false, configureOidcAuthority: true);
        oidcFactory.PrepareDatabase();
        using var client = oidcFactory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/auth/register",
            new { tenantName = "Oidc Delivery", ownerEmail = "owner@oidc.example" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.NotEqual(Guid.Empty, body.RootElement.GetProperty("tenantId").GetGuid());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("accessToken").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("tokenType").ValueKind);
    }
}
