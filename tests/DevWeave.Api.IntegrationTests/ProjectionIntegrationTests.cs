using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevWeave.Contracts.Identity.V1;
using DevWeave.Contracts.Knowledge.V1;
using DevWeave.Contracts.Lifecycle.V1;
using DevWeave.Contracts.Projects.V1;
using DevWeave.Contracts.Projections.V1;
using DevWeave.Contracts.Schema.V1;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Schema;

namespace DevWeave.Api.IntegrationTests;

public sealed class ProjectionIntegrationTests : IClassFixture<IdentityTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };

    private readonly IdentityTestFactory _factory;
    private readonly HttpClient _client;

    public ProjectionIntegrationTests(IdentityTestFactory factory)
    {
        _factory = factory;
        _factory.PrepareDatabase();
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Owner_and_investor_projections_differ_without_leaking_restricted()
    {
        var tenant = await CreateTenantAsync("Projection Corp");
        var owner = await CreateOwnerAsync(tenant.Id, "owner@projection.test");
        var outsider = await CreatePrincipalAsync(tenant.Id, "outsider@projection.test");
        var profile = await CreateLifecycleProfileAsync(tenant.Id, "projection-agile", "Projection Agile", "1.0");
        var project = await CreateProjectAsync(tenant.Id, "projection-project", profile.Id, owner.Id);
        await ActivateTenantMembershipAsync(tenant.Id, outsider.Id, MembershipRole.Member);
        await SeedPublishedSchemaAsync("architecture", "1.0", "{\"type\":\"object\"}");

        var visible = await SeedKnowledgeItemAsync(
            tenant.Id, project.Id, "visible-architecture", owner.Id, Classification.Internal);
        var hidden = await SeedKnowledgeItemAsync(
            tenant.Id, project.Id, "restricted-architecture", owner.Id, Classification.Restricted);

        using var ownerClient = _factory.CreateAuthenticatedHumanClient(tenant.Id, owner.Id);
        using var outsiderClient = _factory.CreateAuthenticatedHumanClient(tenant.Id, outsider.Id);

        using var ownerResponse = await ownerClient.GetAsync(
            $"/tenants/{tenant.Id}/projects/{project.Id}/projections/owner/overview");
        Assert.True(ownerResponse.IsSuccessStatusCode, await ownerResponse.Content.ReadAsStringAsync());
        var ownerProjection = (await ownerResponse.Content.ReadFromJsonAsync<StakeholderProjectionResponse>(JsonOptions))!;
        Assert.Equal("Owner", ownerProjection.Profile);
        Assert.Equal("overview", ownerProjection.Plan);
        Assert.Contains(ownerProjection.Nodes, node => NodeMatches(node, visible.Id));
        Assert.Contains(ownerProjection.Nodes, node => NodeMatches(node, hidden.Id));
        Assert.Contains(ownerProjection.SummaryInputs, resource => resource.Identifier == hidden.Id.ToString("D"));

        using var investorResponse = await ownerClient.GetAsync(
            $"/tenants/{tenant.Id}/projects/{project.Id}/projections/investor/overview");
        Assert.True(investorResponse.IsSuccessStatusCode, await investorResponse.Content.ReadAsStringAsync());
        var investorProjection = (await investorResponse.Content.ReadFromJsonAsync<StakeholderProjectionResponse>(JsonOptions))!;
        Assert.Contains(investorProjection.Nodes, node => NodeMatches(node, visible.Id));
        Assert.DoesNotContain(investorProjection.Nodes, node => NodeMatches(node, hidden.Id));
        Assert.DoesNotContain(investorProjection.SummaryInputs, resource => resource.Identifier == hidden.Id.ToString("D"));
        Assert.DoesNotContain(investorProjection.Counts, count => count.Key.Contains("Restricted", StringComparison.Ordinal));
        Assert.Equal(investorProjection.Nodes.Count, investorProjection.Counts.Single(count => count.Key == "nodes").Value);

        using var unknownPlan = await ownerClient.GetAsync(
            $"/tenants/{tenant.Id}/projects/{project.Id}/projections/owner/secret-ledger");
        Assert.Equal(HttpStatusCode.BadRequest, unknownPlan.StatusCode);

        using var outsiderResponse = await outsiderClient.GetAsync(
            $"/tenants/{tenant.Id}/projects/{project.Id}/projections/owner/overview");
        Assert.Equal(HttpStatusCode.NotFound, outsiderResponse.StatusCode);
    }

    private static bool NodeMatches(ProjectionNodeView node, Guid id) =>
        string.Equals(node.Resource.Kind, "KnowledgeItem", StringComparison.Ordinal) &&
        string.Equals(node.Resource.Identifier, id.ToString("D"), StringComparison.OrdinalIgnoreCase);

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

    private async Task<PrincipalResponse> CreatePrincipalAsync(Guid tenantId, string email)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/principals",
            new CreatePrincipalRequest(email, PrincipalType.Human),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PrincipalResponse>(JsonOptions))!;
    }

    private async Task ActivateTenantMembershipAsync(Guid tenantId, Guid principalId, MembershipRole role)
    {
        using var invite = await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/members",
            new InviteMemberRequest(principalId, role),
            JsonOptions);
        invite.EnsureSuccessStatusCode();
        var membership = (await invite.Content.ReadFromJsonAsync<MembershipResponse>(JsonOptions))!;

        using var activate = await _client.PostAsync(
            $"/tenants/{tenantId}/members/{membership.Id}/activate",
            content: null);
        activate.EnsureSuccessStatusCode();
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

    private async Task<ProjectResponse> CreateProjectAsync(Guid tenantId, string key, Guid lifecycleProfileId, Guid createdBy)
    {
        var client = _factory.CreateAuthenticatedHumanClient(tenantId, createdBy);
        using var response = await client.PostAsJsonAsync(
            $"/tenants/{tenantId}/projects",
            new CreateProjectRequest("Projection Project", key, null, lifecycleProfileId, createdBy),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions))!;
    }

    private async Task<KnowledgeItemResponse> SeedKnowledgeItemAsync(
        Guid tenantId,
        Guid projectId,
        string key,
        Guid createdBy,
        Classification classification = Classification.Internal)
    {
        var client = _factory.CreateAuthenticatedHumanClient(tenantId, createdBy);
        var draft = new CreateKnowledgeItemRequest(
            key, "Title", classification, RepresentationKind.Text, "architecture", "1.0", createdBy);

        using var response = await client.PostAsJsonAsync($"/tenants/{tenantId}/projects/{projectId}/knowledge", draft, JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<KnowledgeItemResponse>(JsonOptions))!;
    }

    private async Task SeedPublishedSchemaAsync(string @namespace, string version, string document)
    {
        var existing = await FindSchemaAsync(@namespace, version);
        if (existing is not null)
        {
            if (existing.Status != SchemaStatus.Published)
                await PublishSchemaAsync(existing.Id);
            return;
        }

        var created = await RegisterSchemaAsync(@namespace, version, document);
        await PublishSchemaAsync(created.Id);
    }

    private async Task<SchemaResponse?> FindSchemaAsync(string @namespace, string version)
    {
        using var response = await _client.GetAsync("/schemas");
        response.EnsureSuccessStatusCode();
        var schemas = await response.Content.ReadFromJsonAsync<List<SchemaResponse>>(JsonOptions);
        return schemas?.FirstOrDefault(s => s.Namespace == @namespace && s.Version == version);
    }

    private async Task<SchemaResponse> RegisterSchemaAsync(string @namespace, string version, string document)
    {
        using var response = await _client.PostAsJsonAsync(
            "/schemas",
            new RegisterSchemaRequest(@namespace, version, "platform", "Integration test schema", document),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SchemaResponse>(JsonOptions))!;
    }

    private async Task<SchemaResponse> PublishSchemaAsync(Guid id)
    {
        using var response = await _client.PostAsync($"/schemas/{id}/publish", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SchemaResponse>(JsonOptions))!;
    }
}
