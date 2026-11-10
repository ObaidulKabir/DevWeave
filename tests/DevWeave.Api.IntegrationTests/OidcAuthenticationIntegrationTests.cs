using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevWeave.Api.Security;
using DevWeave.Application.Abstractions;
using DevWeave.Contracts.Identity.V1;
using DevWeave.Contracts.Lifecycle.V1;
using DevWeave.Contracts.Projects.V1;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace DevWeave.Api.IntegrationTests;

public sealed class OidcAuthenticationIntegrationTests(IdentityTestFactory factory)
    : IClassFixture<IdentityTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public async Task OIDC_human_token_with_required_identity_claims_authenticates()
    {
        using var client = AuthenticatedClient(
            factory,
            IdentityTestFactory.IssueTestOidcToken(Guid.NewGuid(), Guid.NewGuid()));

        using var response = await client.GetAsync("/auth/whoami");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Human", body.RootElement.GetProperty("principalType").GetString());
        Assert.NotNull(body.RootElement.GetProperty("tenantId").GetString());
        Assert.NotNull(body.RootElement.GetProperty("principalId").GetString());
    }

    [Fact]
    public async Task OIDC_token_without_DevWeave_identity_claims_is_rejected()
    {
        using var client = AuthenticatedClient(
            factory,
            IdentityTestFactory.IssueTestOidcToken(
                Guid.NewGuid(),
                Guid.NewGuid(),
                includeIdentityClaims: false));

        using var response = await client.GetAsync("/auth/whoami");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OIDC_token_for_another_audience_is_rejected()
    {
        using var client = AuthenticatedClient(
            factory,
            IdentityTestFactory.IssueTestOidcToken(
                Guid.NewGuid(),
                Guid.NewGuid(),
                audience: "another-api"));

        using var response = await client.GetAsync("/auth/whoami");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OIDC_token_cannot_claim_an_agent_principal_type()
    {
        using var client = AuthenticatedClient(
            factory,
            IdentityTestFactory.IssueTestOidcToken(
                Guid.NewGuid(),
                Guid.NewGuid(),
                principalType: "AiAgent"));

        using var response = await client.GetAsync("/auth/whoami");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Platform_schema_mutations_require_an_explicit_platform_admin_claim()
    {
        using var client = AuthenticatedClient(
            factory,
            IdentityTestFactory.IssueTestOidcToken(Guid.NewGuid(), Guid.NewGuid()));

        using var response = await client.PostAsJsonAsync(
            "/schemas",
            new
            {
                @namespace = "restricted.schema",
                version = "1.0",
                owner = "platform",
                description = "test",
                document = "{\"type\":\"object\"}",
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Tenant_scoped_routes_reject_anonymous_requests()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get, $"/tenants/{Guid.NewGuid()}/knowledge-items");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_tenant_cannot_select_a_different_tenant_in_the_route()
    {
        var tokenTenant = Guid.NewGuid();
        using var client = AuthenticatedClient(
            factory,
            IdentityTestFactory.IssueTestOidcToken(tokenTenant, Guid.NewGuid()));

        using var response = await client.GetAsync($"/tenants/{Guid.NewGuid()}/knowledge-items");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Project_creator_is_derived_from_authenticated_identity_not_request_body()
    {
        factory.PrepareDatabase();
        using var bootstrap = factory.CreateClient();
        using var tenantResponse = await bootstrap.PostAsJsonAsync(
            "/tenants",
            new CreateTenantRequest("Actor attribution tenant"));
        tenantResponse.EnsureSuccessStatusCode();
        var tenant = (await tenantResponse.Content.ReadFromJsonAsync<TenantResponse>(JsonOptions))!;

        using var principalResponse = await bootstrap.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/principals",
            new CreatePrincipalRequest("owner@example.test", PrincipalType.Human));
        principalResponse.EnsureSuccessStatusCode();
        var principal = (await principalResponse.Content.ReadFromJsonAsync<PrincipalResponse>(JsonOptions))!;
        using var client = factory.CreateAuthenticatedHumanClient(tenant.Id, principal.Id);

        using var profileResponse = await client.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/lifecycle-profiles",
            new CreateLifecycleProfileRequest("default", "Default", "1.0"));
        profileResponse.EnsureSuccessStatusCode();
        var profile = (await profileResponse.Content.ReadFromJsonAsync<LifecycleProfileResponse>(JsonOptions))!;

        var spoofedActor = Guid.NewGuid();
        using var projectResponse = await client.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/projects",
            new CreateProjectRequest("Actor attribution", "actor-attribution", null, profile.Id, spoofedActor));

        projectResponse.EnsureSuccessStatusCode();
        var project = (await projectResponse.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions))!;
        Assert.Equal(principal.Id, project.CreatedBy);
        Assert.NotEqual(spoofedActor, project.CreatedBy);
    }

    [Fact]
    public async Task Tenant_routes_hide_resources_until_local_principal_has_active_membership()
    {
        using var securedFactory = new IdentityTestFactory(enforceTenantMembership: true);
        securedFactory.PrepareDatabase();

        using var bootstrap = securedFactory.CreateClient();
        using var tenantResponse = await bootstrap.PostAsJsonAsync(
            "/tenants",
            new CreateTenantRequest("Membership authorization tenant"));
        tenantResponse.EnsureSuccessStatusCode();
        var tenant = (await tenantResponse.Content.ReadFromJsonAsync<TenantResponse>(JsonOptions))!;

        var principal = Principal.Create(
            new TenantId(tenant.Id),
            "member@example.test",
            PrincipalType.Human);
        var membership = Membership.Invite(
            new TenantId(tenant.Id),
            principal.Id,
            MembershipRole.Owner);
        var ordinaryPrincipal = Principal.Create(
            new TenantId(tenant.Id),
            "ordinary@example.test",
            PrincipalType.Human);
        var ordinaryMembership = Membership.Invite(
            new TenantId(tenant.Id),
            ordinaryPrincipal.Id,
            MembershipRole.Member);
        ordinaryMembership.Activate();

        using (var scope = securedFactory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenant.Id;
            var principals = scope.ServiceProvider.GetRequiredService<IPrincipalRepository>();
            var memberships = scope.ServiceProvider.GetRequiredService<IMembershipRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await principals.AddAsync(principal);
            await memberships.AddAsync(membership);
            await principals.AddAsync(ordinaryPrincipal);
            await memberships.AddAsync(ordinaryMembership);
            await unitOfWork.SaveChangesAsync();
        }

        using var client = securedFactory.CreateAuthenticatedHumanClient(tenant.Id, principal.Id.Value);
        using var invitedResponse = await client.GetAsync($"/tenants/{tenant.Id}");
        Assert.Equal(HttpStatusCode.NotFound, invitedResponse.StatusCode);

        using (var scope = securedFactory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenant.Id;
            var memberships = scope.ServiceProvider.GetRequiredService<IMembershipRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            // The tracked entity is reloaded so activation is persisted through the repository.
            var tracked = await memberships.GetByIdAsync(
                new TenantId(tenant.Id),
                membership.Id);
            tracked!.Activate();
            await unitOfWork.SaveChangesAsync();
        }

        using var activeResponse = await client.GetAsync($"/tenants/{tenant.Id}");
        Assert.Equal(HttpStatusCode.OK, activeResponse.StatusCode);

        using var ordinaryClient = securedFactory.CreateAuthenticatedHumanClient(
            tenant.Id,
            ordinaryPrincipal.Id.Value);
        using var membersResponse = await ordinaryClient.GetAsync($"/tenants/{tenant.Id}/members");
        Assert.Equal(HttpStatusCode.NotFound, membersResponse.StatusCode);

        using var profileResponse = await client.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/lifecycle-profiles",
            new CreateLifecycleProfileRequest("project-scope", "Project Scope", "1.0"),
            JsonOptions);
        profileResponse.EnsureSuccessStatusCode();
        var profile = (await profileResponse.Content.ReadFromJsonAsync<LifecycleProfileResponse>(JsonOptions))!;
        using var projectResponse = await client.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/projects",
            new CreateProjectRequest("Scoped project", "scoped-project", null, profile.Id, principal.Id.Value),
            JsonOptions);
        projectResponse.EnsureSuccessStatusCode();
        var project = (await projectResponse.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions))!;

        using var noProjectMembership = await client.GetAsync(
            $"/tenants/{tenant.Id}/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.NotFound, noProjectMembership.StatusCode);

        using (var scope = securedFactory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenant.Id;
            var projectMemberships = scope.ServiceProvider.GetRequiredService<IProjectMembershipRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await projectMemberships.AddAsync(ProjectMembership.Grant(
                new TenantId(tenant.Id),
                new ProjectId(project.Id),
                principal.Id,
                ProjectRole.Viewer));
            await unitOfWork.SaveChangesAsync();
        }

        using var projectRead = await client.GetAsync($"/tenants/{tenant.Id}/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.OK, projectRead.StatusCode);
        using var viewerWrite = await client.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/projects/{project.Id}/references",
            new AddProjectReferenceRequest(
                ProjectReferenceType.Repository,
                "ticket-123",
                null),
            JsonOptions);
        Assert.Equal(HttpStatusCode.NotFound, viewerWrite.StatusCode);
    }

    private static HttpClient AuthenticatedClient(IdentityTestFactory factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
