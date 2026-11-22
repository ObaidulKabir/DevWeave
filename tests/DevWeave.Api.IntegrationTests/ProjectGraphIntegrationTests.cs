using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Application.ProjectGraph;
using DevWeave.Contracts.Identity.V1;
using DevWeave.Contracts.Knowledge.V1;
using DevWeave.Contracts.Lifecycle.V1;
using DevWeave.Contracts.Projects.V1;
using DevWeave.Contracts.Relations.V1;
using DevWeave.Contracts.Schema.V1;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Schema;
using Microsoft.Extensions.DependencyInjection;

namespace DevWeave.Api.IntegrationTests;

public sealed class ProjectGraphIntegrationTests : IClassFixture<IdentityTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };

    private readonly IdentityTestFactory _factory;
    private readonly HttpClient _client;

    public ProjectGraphIntegrationTests(IdentityTestFactory factory)
    {
        _factory = factory;
        _factory.PrepareDatabase();
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Project_graph_returns_authorized_nodes_edges_and_optional_paths()
    {
        var tenant = await CreateTenantAsync("Graph Corp");
        var owner = await CreateOwnerAsync(tenant.Id, "owner@graph.test");
        var member = await CreatePrincipalAsync(tenant.Id, "member@graph.test");
        var outsider = await CreatePrincipalAsync(tenant.Id, "outsider@graph.test");
        var profile = await CreateLifecycleProfileAsync(tenant.Id, "graph-agile", "Graph Agile", "1.0");
        var project = await CreateProjectAsync(tenant.Id, "graph-project", profile.Id, owner.Id);

        await ActivateTenantMembershipAsync(tenant.Id, member.Id, MembershipRole.Member);
        await ActivateTenantMembershipAsync(tenant.Id, outsider.Id, MembershipRole.Member);
        await GrantProjectMembershipAsync(tenant.Id, project.Id, member.Id, ProjectRole.Member);
        await SeedPublishedSchemaAsync("architecture", "1.0", "{\"type\":\"object\"}");

        var visible = await SeedKnowledgeItemAsync(
            tenant.Id, project.Id, "visible-architecture", owner.Id, Classification.Internal);
        var hidden = await SeedKnowledgeItemAsync(
            tenant.Id, project.Id, "restricted-architecture", owner.Id, Classification.Restricted);
        await CreateRelationshipAsync(tenant.Id, project.Id, visible.Id, hidden.Id, owner.Id);

        using var ownerClient = _factory.CreateAuthenticatedHumanClient(tenant.Id, owner.Id);
        using var memberClient = _factory.CreateAuthenticatedHumanClient(tenant.Id, member.Id);
        using var outsiderClient = _factory.CreateAuthenticatedHumanClient(tenant.Id, outsider.Id);

        using (var scope = _factory.Services.CreateScope())
        {
            var projection = scope.ServiceProvider.GetRequiredService<IProjectGraphProjection>();
            var ownerScope = new AuthorizedScope(
                new TenantId(tenant.Id),
                new PrincipalId(owner.Id),
                new ProjectId(project.Id),
                "Inspect project graph",
                "graph-test");
            var snapshot = await projection.BuildAsync(ownerScope);
            Assert.Contains(snapshot.Nodes, node => node.Resource.Identifier == visible.Id.ToString("D"));
            Assert.Contains(snapshot.Nodes, node => node.Resource.Identifier == hidden.Id.ToString("D"));
            Assert.NotEmpty(snapshot.Edges);
        }

        using var ownerResponse = await ownerClient.GetAsync(
            $"/tenants/{tenant.Id}/projects/{project.Id}/graph?startKind=KnowledgeItem&startId={visible.Id:D}&maxDepth=2");
        Assert.True(
            ownerResponse.IsSuccessStatusCode,
            await ownerResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, ownerResponse.StatusCode);
        var ownerGraph = await ownerResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(ownerGraph.TryGetProperty("nodes", out var ownerNodes));
        Assert.True(ownerGraph.TryGetProperty("edges", out var ownerEdges));
        Assert.True(ownerGraph.TryGetProperty("paths", out var ownerPaths));
        Assert.Contains(
            ownerNodes.EnumerateArray(),
            node => NodeMatches(node, "KnowledgeItem", visible.Id));
        Assert.Contains(
            ownerNodes.EnumerateArray(),
            node => NodeMatches(node, "KnowledgeItem", hidden.Id));
        Assert.NotEmpty(ownerEdges.EnumerateArray());
        Assert.NotEmpty(ownerPaths.EnumerateArray());

        using var memberResponse = await memberClient.GetAsync(
            $"/tenants/{tenant.Id}/projects/{project.Id}/graph");
        Assert.Equal(HttpStatusCode.OK, memberResponse.StatusCode);
        var memberGraph = await memberResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var memberNodes = memberGraph.GetProperty("nodes").EnumerateArray().ToArray();
        Assert.Contains(memberNodes, node => NodeMatches(node, "KnowledgeItem", visible.Id));
        Assert.DoesNotContain(memberNodes, node => NodeMatches(node, "KnowledgeItem", hidden.Id));
        Assert.DoesNotContain(
            memberGraph.GetProperty("edges").EnumerateArray(),
            edge => ResourceMatches(edge.GetProperty("to"), "KnowledgeItem", hidden.Id) ||
                    ResourceMatches(edge.GetProperty("from"), "KnowledgeItem", hidden.Id));

        using var outsiderResponse = await outsiderClient.GetAsync(
            $"/tenants/{tenant.Id}/projects/{project.Id}/graph");
        Assert.Equal(HttpStatusCode.NotFound, outsiderResponse.StatusCode);
    }

    private static bool NodeMatches(JsonElement node, string kind, Guid id) =>
        ResourceMatches(node.GetProperty("resource"), kind, id);

    private static bool ResourceMatches(JsonElement resource, string kind, Guid id) =>
        string.Equals(resource.GetProperty("kind").GetString(), kind, StringComparison.Ordinal) &&
        string.Equals(resource.GetProperty("identifier").GetString(), id.ToString("D"), StringComparison.OrdinalIgnoreCase);

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

    private async Task GrantProjectMembershipAsync(
        Guid tenantId,
        Guid projectId,
        Guid principalId,
        ProjectRole role)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/projects/{projectId}/members",
            new GrantProjectMembershipRequest(principalId, role),
            JsonOptions);
        response.EnsureSuccessStatusCode();
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
            new CreateProjectRequest("Graph Project", key, null, lifecycleProfileId, createdBy),
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

    private async Task CreateRelationshipAsync(
        Guid tenantId,
        Guid projectId,
        Guid sourceItemId,
        Guid targetItemId,
        Guid actorId)
    {
        using var client = _factory.CreateAuthenticatedHumanClient(tenantId, actorId);
        using var response = await client.PostAsJsonAsync(
            $"/tenants/{tenantId}/projects/{projectId}/relationships",
            new CreateRelationshipRequest(sourceItemId, targetItemId, "references", actorId),
            JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    private async Task SeedPublishedSchemaAsync(string @namespace, string version, string document)
    {
        var existing = await FindSchemaAsync(@namespace, version);
        if (existing is not null)
        {
            if (existing.Status != SchemaStatus.Published)
            {
                await PublishSchemaAsync(existing.Id);
            }

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
