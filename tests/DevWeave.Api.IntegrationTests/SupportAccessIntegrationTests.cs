using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevWeave.Contracts.Identity.V1;
using DevWeave.Domain.Identity;

namespace DevWeave.Api.IntegrationTests;

/// <summary>Proves TEN-009: no standing platform support access without an active grant.</summary>
public sealed class SupportAccessIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public async Task Platform_admin_without_membership_or_grant_cannot_read_tenant_members()
    {
        using var factory = new IdentityTestFactory(enforceTenantMembership: true);
        factory.PrepareDatabase();

        using var bootstrap = factory.CreateClient();
        using var register = await bootstrap.PostAsJsonAsync(
            "/auth/register",
            new { tenantName = "Support Access Co", ownerEmail = "owner@support.example" });
        register.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await register.Content.ReadAsStringAsync());
        var tenantId = body.RootElement.GetProperty("tenantId").GetGuid();
        var ownerId = body.RootElement.GetProperty("principalId").GetGuid();

        // Tenant owner (member) manages grants; platform_admin alone is insufficient without a grant.
        using var ownerClient = factory.CreateClient();
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            IdentityTestFactory.IssueTestOidcToken(tenantId, ownerId));

        using var operatorResponse = await ownerClient.PostAsJsonAsync(
            $"/tenants/{tenantId}/principals",
            new CreatePrincipalRequest("operator@support.example", PrincipalType.Human),
            JsonOptions);
        operatorResponse.EnsureSuccessStatusCode();
        var operatorPrincipal = (await operatorResponse.Content.ReadFromJsonAsync<PrincipalResponse>(JsonOptions))!;

        using var deniedClient = factory.CreateClient();
        deniedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            IdentityTestFactory.IssueTestOidcToken(tenantId, operatorPrincipal.Id, isPlatformAdmin: true));

        using var denied = await deniedClient.GetAsync($"/tenants/{tenantId}/members");
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);

        using var issue = await ownerClient.PostAsJsonAsync(
            $"/tenants/{tenantId}/support-access",
            new { operatorId = operatorPrincipal.Id, purpose = "Incident triage", lifetimeHours = 4 });
        Assert.Equal(HttpStatusCode.Created, issue.StatusCode);

        using var allowed = await deniedClient.GetAsync($"/tenants/{tenantId}/members");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);

        using var bodyGrant = JsonDocument.Parse(await issue.Content.ReadAsStringAsync());
        var grantId = bodyGrant.RootElement.GetProperty("id").GetGuid();

        using var revoke = await ownerClient.PostAsync(
            $"/tenants/{tenantId}/support-access/{grantId}/revoke",
            content: null);
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        using var deniedAfterRevoke = await deniedClient.GetAsync($"/tenants/{tenantId}/members");
        Assert.Equal(HttpStatusCode.NotFound, deniedAfterRevoke.StatusCode);
    }

    [Fact]
    public async Task Platform_admin_with_expired_grant_cannot_read_tenant_members()
    {
        using var factory = new IdentityTestFactory(enforceTenantMembership: true);
        factory.PrepareDatabase();

        using var bootstrap = factory.CreateClient();
        using var register = await bootstrap.PostAsJsonAsync(
            "/auth/register",
            new { tenantName = "Expired Grant Co", ownerEmail = "owner@expired.example" });
        register.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await register.Content.ReadAsStringAsync());
        var tenantId = body.RootElement.GetProperty("tenantId").GetGuid();
        var ownerId = body.RootElement.GetProperty("principalId").GetGuid();

        using var ownerClient = factory.CreateClient();
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            IdentityTestFactory.IssueTestOidcToken(tenantId, ownerId));

        using var operatorResponse = await ownerClient.PostAsJsonAsync(
            $"/tenants/{tenantId}/principals",
            new CreatePrincipalRequest("operator@expired.example", PrincipalType.Human),
            JsonOptions);
        operatorResponse.EnsureSuccessStatusCode();
        var operatorPrincipal = (await operatorResponse.Content.ReadFromJsonAsync<PrincipalResponse>(JsonOptions))!;

        // lifetimeHours <= 0 is coerced to 8h by the endpoint; issue then revoke immediately to simulate expiry denial.
        using var issue = await ownerClient.PostAsJsonAsync(
            $"/tenants/{tenantId}/support-access",
            new { operatorId = operatorPrincipal.Id, purpose = "Short window", lifetimeHours = 1 });
        Assert.Equal(HttpStatusCode.Created, issue.StatusCode);
        using var grantBody = JsonDocument.Parse(await issue.Content.ReadAsStringAsync());
        var grantId = grantBody.RootElement.GetProperty("id").GetGuid();
        using var revoke = await ownerClient.PostAsync(
            $"/tenants/{tenantId}/support-access/{grantId}/revoke",
            content: null);
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        using var deniedClient = factory.CreateClient();
        deniedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            IdentityTestFactory.IssueTestOidcToken(tenantId, operatorPrincipal.Id, isPlatformAdmin: true));

        using var denied = await deniedClient.GetAsync($"/tenants/{tenantId}/members");
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
    }
}
