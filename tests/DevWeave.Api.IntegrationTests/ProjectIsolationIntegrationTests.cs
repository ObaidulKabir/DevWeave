using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevWeave.Application.Abstractions;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Contracts.Identity.V1;
using DevWeave.Contracts.Knowledge.V1;
using DevWeave.Contracts.Planned.V1;
using DevWeave.Contracts.Lifecycle.V1;
using DevWeave.Contracts.Projects.V1;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Schema;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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

        // Creating a project already grants its creator the first Owner membership.
        using var membershipScope = _factory.Services.CreateScope();
        var membershipRepository = membershipScope.ServiceProvider.GetRequiredService<IProjectMembershipRepository>();
        var membershipA = (await membershipRepository.ListByProjectAsync(new TenantId(alpha.Id), new ProjectId(projectA.Id))).Single();
        var membershipB = (await membershipRepository.ListByProjectAsync(new TenantId(beta.Id), new ProjectId(projectB.Id))).Single();

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
        Assert.Equal(membershipA.Id, scopedMemberships[0].Id);
        Assert.DoesNotContain(scopedMemberships, m => m.Id == membershipB.Id);
        Assert.Empty(await memberships.ListByProjectAsync(new TenantId(alpha.Id), new ProjectId(projectB.Id)));
        Assert.Null(await memberships.GetByIdAsync(new TenantId(alpha.Id), membershipB.Id));
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

    [Fact]
    public async Task COM_016_a_client_member_cannot_read_another_project_in_the_same_tenant()
    {
        var tenant = await CreateTenantAsync("Agency");
        var owner = await CreatePrincipalAsync(tenant.Id, $"owner-{Guid.NewGuid():N}@example.test", PrincipalType.Human);
        var client = await CreatePrincipalAsync(tenant.Id, $"client-{Guid.NewGuid():N}@example.test", PrincipalType.Human);
        var profile = await CreateLifecycleProfileAsync(tenant.Id, $"plan-{Guid.NewGuid():N}", "Plan", "1");

        using (var invite = await _client.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/members",
            new InviteMemberRequest(client.Id, MembershipRole.Member),
            JsonOptions))
            invite.EnsureSuccessStatusCode();

        var shop = await CreateProjectAsync(tenant.Id, $"shop-{Guid.NewGuid():N}"[..12], profile.Id, owner.Id);
        var billing = await CreateProjectAsync(tenant.Id, $"bill-{Guid.NewGuid():N}"[..12], profile.Id, owner.Id);
        await GrantMembershipAsync(tenant.Id, shop.Id, client.Id, ProjectRole.Viewer);

        using var clientHttp = _factory.CreateAuthenticatedHumanClient(tenant.Id, client.Id);
        using var own = await clientHttp.GetAsync($"/tenants/{tenant.Id}/projects/{shop.Id}");
        using var other = await clientHttp.GetAsync($"/tenants/{tenant.Id}/projects/{billing.Id}");

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
    }

    [Fact]
    public async Task BUS_AC_01_two_client_projects_keep_knowledge_repositories_and_reports_separate()
    {
        var tenant = await CreateTenantAsync("Agency");
        var owner = await CreatePrincipalAsync(tenant.Id, $"lead-{Guid.NewGuid():N}@example.test", PrincipalType.Human);
        var clientA = await CreatePrincipalAsync(tenant.Id, $"a-{Guid.NewGuid():N}@example.test", PrincipalType.Human);
        var clientB = await CreatePrincipalAsync(tenant.Id, $"b-{Guid.NewGuid():N}@example.test", PrincipalType.Human);
        var profile = await CreateLifecycleProfileAsync(tenant.Id, $"plan-{Guid.NewGuid():N}", "Plan", "1");

        using (var inviteA = await _client.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/members",
            new InviteMemberRequest(clientA.Id, MembershipRole.Member),
            JsonOptions))
            inviteA.EnsureSuccessStatusCode();
        using (var inviteB = await _client.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/members",
            new InviteMemberRequest(clientB.Id, MembershipRole.Member),
            JsonOptions))
            inviteB.EnsureSuccessStatusCode();

        var shop = await CreateProjectAsync(tenant.Id, $"shop-{Guid.NewGuid():N}"[..12], profile.Id, owner.Id);
        var billing = await CreateProjectAsync(tenant.Id, $"bill-{Guid.NewGuid():N}"[..12], profile.Id, owner.Id);
        await GrantMembershipAsync(tenant.Id, shop.Id, clientA.Id, ProjectRole.Viewer);
        await GrantMembershipAsync(tenant.Id, billing.Id, clientB.Id, ProjectRole.Viewer);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
            if (!await db.SchemaDefinitions.AnyAsync(schema => schema.Namespace == "doc.note" && schema.Version == "1.0"))
            {
                var schema = SchemaDefinition.Create("doc.note", "1.0", "platform", "Note", "{\"type\":\"object\"}");
                schema.Publish();
                db.Add(schema);
                await db.SaveChangesAsync();
            }
        }

        using var ownerHttp = _factory.CreateAuthenticatedHumanClient(tenant.Id, owner.Id);
        await AddReferenceAsAsync(ownerHttp, tenant.Id, shop.Id, "acme/shop");
        await AddReferenceAsAsync(ownerHttp, tenant.Id, billing.Id, "acme/billing");
        using (var knowledge = await ownerHttp.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/projects/{shop.Id}/knowledge",
            new DevWeave.Contracts.Knowledge.V1.CreateKnowledgeItemRequest(
                "shop-note", "Shop checkout", DevWeave.Domain.Knowledge.Classification.Internal,
                DevWeave.Domain.Knowledge.RepresentationKind.Text, "doc.note", "1.0", owner.Id),
            JsonOptions))
            knowledge.EnsureSuccessStatusCode();
        using (var knowledge = await ownerHttp.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/projects/{billing.Id}/knowledge",
            new DevWeave.Contracts.Knowledge.V1.CreateKnowledgeItemRequest(
                "bill-note", "Billing ledger", DevWeave.Domain.Knowledge.Classification.Internal,
                DevWeave.Domain.Knowledge.RepresentationKind.Text, "doc.note", "1.0", owner.Id),
            JsonOptions))
            knowledge.EnsureSuccessStatusCode();

        using var httpA = _factory.CreateAuthenticatedHumanClient(tenant.Id, clientA.Id);
        using var httpB = _factory.CreateAuthenticatedHumanClient(tenant.Id, clientB.Id);

        var visibleToA = await ListProjectsAsAsync(httpA, tenant.Id);
        Assert.Single(visibleToA);
        Assert.Equal(shop.Id, visibleToA[0].Id);
        var visibleToB = await ListProjectsAsAsync(httpB, tenant.Id);
        Assert.Single(visibleToB);
        Assert.Equal(billing.Id, visibleToB[0].Id);
        var visibleToLead = await ListProjectsAsAsync(ownerHttp, tenant.Id);
        Assert.Contains(visibleToLead, project => project.Id == shop.Id);
        Assert.Contains(visibleToLead, project => project.Id == billing.Id);

        Assert.Equal(HttpStatusCode.NotFound, (await httpA.GetAsync($"/tenants/{tenant.Id}/projects/{billing.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await httpB.GetAsync($"/tenants/{tenant.Id}/projects/{shop.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await httpA.GetAsync($"/tenants/{tenant.Id}/projects/{billing.Id}/metrics")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await httpB.GetAsync($"/tenants/{tenant.Id}/projects/{shop.Id}/metrics")).StatusCode);

        var shopKnowledge = await httpA.GetStringAsync($"/tenants/{tenant.Id}/projects/{shop.Id}/knowledge");
        var billingKnowledgeForA = await httpA.GetStringAsync($"/tenants/{tenant.Id}/projects/{billing.Id}/knowledge");
        Assert.Contains("Shop checkout", shopKnowledge, StringComparison.Ordinal);
        Assert.DoesNotContain("Billing ledger", billingKnowledgeForA, StringComparison.Ordinal);
        var billingKnowledge = await httpB.GetStringAsync($"/tenants/{tenant.Id}/projects/{billing.Id}/knowledge");
        Assert.Contains("Billing ledger", billingKnowledge, StringComparison.Ordinal);
        Assert.DoesNotContain("Shop checkout", billingKnowledge, StringComparison.Ordinal);

        using var crossedReference = await httpA.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/projects/{billing.Id}/references",
            new AddProjectReferenceRequest(ProjectReferenceType.Repository, "acme/stolen", null),
            JsonOptions);
        Assert.Equal(HttpStatusCode.NotFound, crossedReference.StatusCode);
    }

    [Fact]
    public async Task BUS_AC_07_a_client_stakeholder_does_not_receive_private_agent_messages()
    {
        var tenant = await CreateTenantAsync("Agency");
        var owner = await CreatePrincipalAsync(tenant.Id, $"lead-{Guid.NewGuid():N}@example.test", PrincipalType.Human);
        var client = await CreatePrincipalAsync(tenant.Id, $"client-{Guid.NewGuid():N}@example.test", PrincipalType.Human);
        var agent = await CreatePrincipalAsync(tenant.Id, $"agent-{Guid.NewGuid():N}@example.test", PrincipalType.AiAgent);
        var profile = await CreateLifecycleProfileAsync(tenant.Id, $"plan-{Guid.NewGuid():N}", "Plan", "1");
        using (var invite = await _client.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/members",
            new InviteMemberRequest(client.Id, MembershipRole.Member),
            JsonOptions))
            invite.EnsureSuccessStatusCode();

        var shop = await CreateProjectAsync(tenant.Id, $"shop-{Guid.NewGuid():N}"[..12], profile.Id, owner.Id);
        await GrantMembershipAsync(tenant.Id, shop.Id, client.Id, ProjectRole.Viewer);
        await GrantMembershipAsync(tenant.Id, shop.Id, agent.Id, ProjectRole.Member);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
            if (!await db.SchemaDefinitions.AnyAsync(schema => schema.Namespace == "doc.note" && schema.Version == "1.0"))
            {
                var schema = SchemaDefinition.Create("doc.note", "1.0", "platform", "Note", "{\"type\":\"object\"}");
                schema.Publish();
                db.Add(schema);
                await db.SaveChangesAsync();
            }
        }

        using var ownerHttp = _factory.CreateAuthenticatedHumanClient(tenant.Id, owner.Id);
        using (var shared = await ownerHttp.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/projects/{shop.Id}/knowledge",
            new CreateKnowledgeItemRequest("shop-brief", "Shared brief", Classification.Internal, RepresentationKind.Text, "doc.note", "1.0", owner.Id),
            JsonOptions))
            shared.EnsureSuccessStatusCode();
        using (var restricted = await ownerHttp.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/projects/{shop.Id}/knowledge",
            new CreateKnowledgeItemRequest("shop-secret", "Restricted ledger", Classification.Restricted, RepresentationKind.Text, "doc.note", "1.0", owner.Id),
            JsonOptions))
            restricted.EnsureSuccessStatusCode();

        Guid channelId;
        using (var scope = _factory.Services.CreateScope())
        {
            var agentScope = new AuthorizedScope(
                new TenantId(tenant.Id), new PrincipalId(agent.Id), new ProjectId(shop.Id), "Send", "trace");
            var registry = scope.ServiceProvider.GetRequiredService<IRuntimeRegistry>();
            var channels = scope.ServiceProvider.GetRequiredService<ICoordinationChannels>();
            var runtimeId = await registry.RegisterAsync(
                agentScope, new RuntimeRegistration(agent.Id, "External", "1.0", Array.Empty<string>(), "session"));
            channelId = await channels.OpenAsync(agentScope, "Direct", null, null);
            await channels.SendAsync(
                agentScope,
                runtimeId,
                new MessageDraft(channelId, "Note", "1.0", "{\"text\":\"private agent note\"}", Array.Empty<ResourceRef>(), null, $"idem-{Guid.NewGuid():N}"));
        }

        var messageUrl = $"/tenants/{tenant.Id}/projects/{shop.Id}/coordination/channels/{channelId}/messages?afterSequence=0&limit=20";
        using var clientHttp = _factory.CreateAuthenticatedHumanClient(tenant.Id, client.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await clientHttp.GetAsync(messageUrl)).StatusCode);

        var visible = await clientHttp.GetStringAsync($"/tenants/{tenant.Id}/projects/{shop.Id}/knowledge");
        Assert.Contains("Shared brief", visible, StringComparison.Ordinal);
        Assert.DoesNotContain("Restricted ledger", visible, StringComparison.Ordinal);

        var ownerMessages = await ownerHttp.GetStringAsync(messageUrl);
        Assert.Contains("private agent note", ownerMessages, StringComparison.Ordinal);
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

    private static async Task AddReferenceAsAsync(HttpClient client, Guid tenantId, Guid projectId, string externalId)
    {
        using var response = await client.PostAsJsonAsync(
            $"/tenants/{tenantId}/projects/{projectId}/references",
            new AddProjectReferenceRequest(ProjectReferenceType.Repository, externalId, null),
            JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<List<ProjectResponse>> ListProjectsAsAsync(HttpClient client, Guid tenantId)
    {
        using var response = await client.GetAsync($"/tenants/{tenantId}/projects");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<ProjectResponse>>(JsonOptions))!;
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
