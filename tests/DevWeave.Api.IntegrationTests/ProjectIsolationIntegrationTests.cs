using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevWeave.Application.Abstractions;
using DevWeave.Contracts.Identity.V1;
using DevWeave.Contracts.Lifecycle.V1;
using DevWeave.Contracts.Projects.V1;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace DevWeave.Api.IntegrationTests;

/// <summary>
/// Proves the project isolation invariants (PRJ-001/PRJ-002/PRJ-003/PRJ-006, LFC-001) end-to-end
/// against a real database: a query scoped to tenant A must never disclose tenant B projects,
/// references, memberships or lifecycle profiles, across every read path (deny-by-default).
/// </summary>
public sealed class ProjectIsolationIntegrationTests : IClassFixture<IdentityTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };

    private readonly IdentityTestFactory _factory;
    private readonly HttpClient _client;

    public ProjectIsolationIntegrationTests(IdentityTestFactory factory)
    {
        _factory = factory;
        _factory.PrepareDatabase();
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task PRJ_002_project_reference_and_membership_reads_never_disclose_cross_tenant_data()
    {
        var alpha = await CreateTenantAsync("Alpha Corp");
        var beta = await CreateTenantAsync("Beta Corp");

        var principalA = await CreatePrincipalAsync(alpha.Id, "a@alphacorp.com", PrincipalType.Human);
        var principalB = await CreatePrincipalAsync(beta.Id, "b@betacorp.com", PrincipalType.Human);

        var profileA = await CreateLifecycleProfileAsync(alpha.Id, "agile", "Agile", "1.0");
        var profileB = await CreateLifecycleProfileAsync(beta.Id, "agile", "Agile", "1.0");

        var projectA = await CreateProjectAsync(alpha.Id, "shop-api", profileA.Id, principalA.Id);
        var projectB = await CreateProjectAsync(beta.Id, "shop-api", profileB.Id, principalB.Id);

        var referenceA = await AddReferenceAsync(alpha.Id, projectA.Id, ProjectReferenceType.Repository, "devweave/a", null);
        var referenceB = await AddReferenceAsync(beta.Id, projectB.Id, ProjectReferenceType.Repository, "devweave/b", null);

        var membershipA = await GrantMembershipAsync(alpha.Id, projectA.Id, principalA.Id, ProjectRole.Owner);
        var membershipB = await GrantMembershipAsync(beta.Id, projectB.Id, principalB.Id, ProjectRole.Owner);

        // HTTP project read path
        var alphaProjects = await ListProjectsAsync(alpha.Id);
        Assert.Single(alphaProjects);
        Assert.Equal(projectA.Id, alphaProjects[0].Id);
        Assert.DoesNotContain(alphaProjects, p => p.Id == projectB.Id);

        var crossTenantProject = await GetProjectAsync(alpha.Id, projectB.Id);
        Assert.Null(crossTenantProject);

        // Repository read paths (negative disclosure across every read method)
        using var scope = _factory.Services.CreateScope();
        var projects = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var references = scope.ServiceProvider.GetRequiredService<IProjectReferenceRepository>();
        var memberships = scope.ServiceProvider.GetRequiredService<IProjectMembershipRepository>();

        var scopedProjects = await projects.ListByTenantAsync(new TenantId(alpha.Id));
        Assert.Single(scopedProjects);
        Assert.Equal(projectA.Id, scopedProjects[0].Id.Value);
        Assert.DoesNotContain(scopedProjects, p => p.Id.Value == projectB.Id);

        Assert.Null(await projects.GetByIdAsync(new TenantId(alpha.Id), new ProjectId(projectB.Id)));

        var scopedReferences = await references.ListByProjectAsync(new TenantId(alpha.Id), new ProjectId(projectA.Id));
        Assert.Single(scopedReferences);
        Assert.DoesNotContain(scopedReferences, r => r.Id.Value == referenceB.Id);
        Assert.Empty(await references.ListByProjectAsync(new TenantId(alpha.Id), new ProjectId(projectB.Id)));

        var scopedMemberships = await memberships.ListByProjectAsync(new TenantId(alpha.Id), new ProjectId(projectA.Id));
        Assert.Single(scopedMemberships);
        Assert.Equal(membershipA.Id, scopedMemberships[0].Id.Value);
        Assert.DoesNotContain(scopedMemberships, m => m.Id.Value == membershipB.Id);
        Assert.Empty(await memberships.ListByProjectAsync(new TenantId(alpha.Id), new ProjectId(projectB.Id)));
        Assert.Null(await memberships.GetByIdAsync(new TenantId(alpha.Id), new ProjectMembershipId(membershipB.Id)));
    }

    [Fact]
    public async Task LFC_001_lifecycle_profile_reads_never_disclose_cross_tenant_data()
    {
        var alpha = await CreateTenantAsync("Alpha Corp");
        var beta = await CreateTenantAsync("Beta Corp");

        var profileA = await CreateLifecycleProfileAsync(alpha.Id, "agile", "Agile", "1.0");
        var profileB = await CreateLifecycleProfileAsync(beta.Id, "agile", "Agile", "1.0");

        var alphaProfiles = await ListLifecycleProfilesAsync(alpha.Id);
        Assert.Single(alphaProfiles);
        Assert.Equal(profileA.Id, alphaProfiles[0].Id);
        Assert.DoesNotContain(alphaProfiles, p => p.Id == profileB.Id);

        using var scope = _factory.Services.CreateScope();
        var profiles = scope.ServiceProvider.GetRequiredService<ILifecycleProfileRepository>();

        var scoped = await profiles.ListByTenantAsync(new TenantId(alpha.Id));
        Assert.Single(scoped);
        Assert.DoesNotContain(scoped, p => p.Id.Value == profileB.Id);

        Assert.Null(await profiles.GetByIdAsync(new TenantId(alpha.Id), new LifecycleProfileId(profileB.Id)));
    }

    private async Task<TenantResponse> CreateTenantAsync(string name)
    {
        using var response = await _client.PostAsJsonAsync("/tenants", new CreateTenantRequest(name), JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TenantResponse>(JsonOptions))!;
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

    private async Task<LifecycleProfileResponse> CreateLifecycleProfileAsync(Guid tenantId, string key, string name, string version)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/lifecycle-profiles",
            new CreateLifecycleProfileRequest(key, name, version),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LifecycleProfileResponse>(JsonOptions))!;
    }

    private async Task<List<LifecycleProfileResponse>> ListLifecycleProfilesAsync(Guid tenantId)
    {
        using var response = await _client.GetAsync($"/tenants/{tenantId}/lifecycle-profiles");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<LifecycleProfileResponse>>(JsonOptions))!;
    }

    private async Task<ProjectResponse> CreateProjectAsync(Guid tenantId, string key, Guid lifecycleProfileId, Guid createdBy)
    {
        using var client = _factory.CreateAuthenticatedHumanClient(tenantId, createdBy);
        using var response = await client.PostAsJsonAsync(
            $"/tenants/{tenantId}/projects",
            new CreateProjectRequest("Shop API", key, null, lifecycleProfileId, createdBy),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions))!;
    }

    private async Task<List<ProjectResponse>> ListProjectsAsync(Guid tenantId)
    {
        using var response = await _client.GetAsync($"/tenants/{tenantId}/projects");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<ProjectResponse>>(JsonOptions))!;
    }

    private async Task<ProjectResponse?> GetProjectAsync(Guid tenantId, Guid projectId)
    {
        using var response = await _client.GetAsync($"/tenants/{tenantId}/projects/{projectId}");
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions);
    }

    private async Task<ProjectReferenceResponse> AddReferenceAsync(Guid tenantId, Guid projectId, ProjectReferenceType type, string externalId, string? url)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/projects/{projectId}/references",
            new AddProjectReferenceRequest(type, externalId, url),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectReferenceResponse>(JsonOptions))!;
    }

    private async Task<ProjectMembershipResponse> GrantMembershipAsync(Guid tenantId, Guid projectId, Guid principalId, ProjectRole role)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/projects/{projectId}/members",
            new GrantProjectMembershipRequest(principalId, role),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectMembershipResponse>(JsonOptions))!;
    }
}
