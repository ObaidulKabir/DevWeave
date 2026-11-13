using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevWeave.Application.Abstractions;
using DevWeave.Contracts.Identity.V1;
using DevWeave.Contracts.Knowledge.V1;
using DevWeave.Contracts.Lifecycle.V1;
using DevWeave.Contracts.Projects.V1;
using DevWeave.Contracts.Relations.V1;
using DevWeave.Contracts.Schema.V1;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Relations;
using DevWeave.Domain.Schema;
using Microsoft.Extensions.DependencyInjection;

namespace DevWeave.Api.IntegrationTests;

/// <summary>
/// Proves the relationships surface (SRS REL-001..REL-005, REL-007, SCH-007, BR-007) end-to-end
/// against a real database: exactly 11 canonical relation types are seeded and discoverable, an
/// unknown type is rejected with 422, a cross-project edge conflicts with 409, traversal returns
/// explainable ordered paths bounded by depth, and every read path never discloses a cross-tenant
/// relationship (negative disclosure).
/// </summary>
public sealed class RelationshipIntegrationTests : IClassFixture<IdentityTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };

    private readonly IdentityTestFactory _factory;
    private readonly HttpClient _client;

    public RelationshipIntegrationTests(IdentityTestFactory factory)
    {
        _factory = factory;
        _factory.PrepareDatabase();
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task REL_003_the_11_canonical_relation_types_are_seeded_and_discoverable()
    {
        var types = await ListRelationTypesAsync();

        Assert.Equal(11, types.Count);
        Assert.Equal(
            RelationTypeCodes.Canonical.OrderBy(c => c, StringComparer.Ordinal),
            types.Select(t => t.Code).OrderBy(c => c, StringComparer.Ordinal));
        Assert.All(types, t =>
        {
            Assert.Equal(RelationTypeStatus.Active, t.Status);
            Assert.Equal(RelationDirection.Forward, t.Direction);
            Assert.Equal(RelationCardinality.ManyToMany, t.Cardinality);
        });
    }

    [Fact]
    public async Task BR_007_an_unknown_relation_type_is_rejected_with_422()
    {
        var tenant = await CreateTenantAsync("Alpha Corp");
        var principal = await CreatePrincipalAsync(tenant.Id, "a@alphacorp.com", PrincipalType.Human);
        var profile = await CreateLifecycleProfileAsync(tenant.Id, "agile", "Agile", "1.0");
        var project = await CreateProjectAsync(tenant.Id, "shop-api", profile.Id, principal.Id);
        var source = await CreateKnowledgeItemAsync(tenant.Id, project.Id, "architecture", principal.Id);
        var target = await CreateKnowledgeItemAsync(tenant.Id, project.Id, "implementation", principal.Id);

        var response = await TryCreateRelationshipAsync(
            tenant.Id, project.Id, source.Id, target.Id, "does_not_exist", principal.Id);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<RelationshipError>(JsonOptions))!;
        Assert.Equal("unknown-relation-type", body.Code);
    }

    [Fact]
    public async Task REL_007_a_cross_project_relationship_is_rejected_with_409()
    {
        var tenant = await CreateTenantAsync("Alpha Corp");
        var principal = await CreatePrincipalAsync(tenant.Id, "a@alphacorp.com", PrincipalType.Human);
        var profile = await CreateLifecycleProfileAsync(tenant.Id, "agile", "Agile", "1.0");
        var projectA = await CreateProjectAsync(tenant.Id, "shop-api-a", profile.Id, principal.Id);
        var projectB = await CreateProjectAsync(tenant.Id, "shop-api-b", profile.Id, principal.Id);
        var source = await CreateKnowledgeItemAsync(tenant.Id, projectA.Id, "architecture", principal.Id);
        var target = await CreateKnowledgeItemAsync(tenant.Id, projectB.Id, "implementation", principal.Id);

        var response = await TryCreateRelationshipAsync(
            tenant.Id, projectA.Id, source.Id, target.Id, "references", principal.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<RelationshipError>(JsonOptions))!;
        Assert.Equal(CrossProjectReferenceException.Code, body.Code);
    }

    [Fact]
    public async Task REL_004_REL_005_traversal_returns_explainable_paths_bounded_by_depth()
    {
        var tenant = await CreateTenantAsync("Alpha Corp");
        var principal = await CreatePrincipalAsync(tenant.Id, "a@alphacorp.com", PrincipalType.Human);
        var profile = await CreateLifecycleProfileAsync(tenant.Id, "agile", "Agile", "1.0");
        var project = await CreateProjectAsync(tenant.Id, "shop-api", profile.Id, principal.Id);

        var r = await CreateKnowledgeItemAsync(tenant.Id, project.Id, "requirement", principal.Id);
        var d = await CreateKnowledgeItemAsync(tenant.Id, project.Id, "design", principal.Id);
        var i = await CreateKnowledgeItemAsync(tenant.Id, project.Id, "implementation", principal.Id);

        await CreateRelationshipAsync(tenant.Id, project.Id, r.Id, d.Id, "references", principal.Id);
        await CreateRelationshipAsync(tenant.Id, project.Id, d.Id, i.Id, "references", principal.Id);

        var result = await TraverseAsync(tenant.Id, r.Id, TraversalDirection.Outgoing, 2, 100);

        Assert.False(result.Truncated);
        Assert.Equal(2, result.Paths.Count);

        var oneHop = Assert.Single(result.Paths, p => p.Depth == 1);
        Assert.Equal(d.Id, oneHop.EndItemId);
        var oneEdge = Assert.Single(oneHop.Edges);
        Assert.Equal((r.Id, d.Id), (oneEdge.FromItemId, oneEdge.ToItemId));

        var twoHop = Assert.Single(result.Paths, p => p.Depth == 2);
        Assert.Equal(i.Id, twoHop.EndItemId);
        Assert.Equal(2, twoHop.Edges.Count);
        Assert.Equal((r.Id, d.Id), (twoHop.Edges[0].FromItemId, twoHop.Edges[0].ToItemId));
        Assert.Equal((d.Id, i.Id), (twoHop.Edges[1].FromItemId, twoHop.Edges[1].ToItemId));
        Assert.All(twoHop.Edges, e => Assert.Equal("references", e.RelationTypeCode));
    }

    [Fact]
    public async Task relationship_reads_never_disclose_cross_tenant_data()
    {
        var alpha = await CreateTenantAsync("Alpha Corp");
        var beta = await CreateTenantAsync("Beta Corp");

        var principalA = await CreatePrincipalAsync(alpha.Id, "a@alphacorp.com", PrincipalType.Human);
        var principalB = await CreatePrincipalAsync(beta.Id, "b@betacorp.com", PrincipalType.Human);

        var profileA = await CreateLifecycleProfileAsync(alpha.Id, "agile", "Agile", "1.0");
        var profileB = await CreateLifecycleProfileAsync(beta.Id, "agile", "Agile", "1.0");

        var projectA = await CreateProjectAsync(alpha.Id, "shop-api", profileA.Id, principalA.Id);
        var projectB = await CreateProjectAsync(beta.Id, "shop-api", profileB.Id, principalB.Id);

        var sourceB = await CreateKnowledgeItemAsync(beta.Id, projectB.Id, "architecture", principalB.Id);
        var targetB = await CreateKnowledgeItemAsync(beta.Id, projectB.Id, "implementation", principalB.Id);
        var edgeB = await CreateRelationshipAsync(beta.Id, projectB.Id, sourceB.Id, targetB.Id, "references", principalB.Id);

        // HTTP read paths must yield zero/404 for tenant A.
        Assert.NotEmpty(await ListRelationshipsForProjectAsync(beta.Id, projectB.Id));
        Assert.Empty(await ListRelationshipsForProjectAsync(alpha.Id, projectB.Id));
        Assert.Null(await GetRelationshipAsync(alpha.Id, edgeB.Id));

        // Repository read paths: negative disclosure across every read method.
        using var scope = _factory.Services.CreateScope();
        var relationships = scope.ServiceProvider.GetRequiredService<IRelationshipRepository>();

        Assert.Null(await relationships.GetByIdAsync(new TenantId(alpha.Id), new RelationshipId(edgeB.Id)));
        Assert.Empty(await relationships.ListByProjectAsync(new TenantId(alpha.Id), new ProjectId(projectB.Id)));
        Assert.Empty(await relationships.ListOutgoingAsync(new TenantId(alpha.Id), new KnowledgeItemId(sourceB.Id)));
        Assert.Empty(await relationships.ListIncomingAsync(new TenantId(alpha.Id), new KnowledgeItemId(targetB.Id)));
    }

    [Fact]
    public async Task Creating_and_revoking_a_relationship_requires_editing_project_membership()
    {
        var tenant = await CreateTenantAsync("Alpha Corp");
        var owner = await CreatePrincipalAsync(tenant.Id, "owner@alphacorp.com", PrincipalType.Human);
        var outsider = await CreatePrincipalAsync(tenant.Id, "outsider@alphacorp.com", PrincipalType.Human);
        var viewer = await CreatePrincipalAsync(tenant.Id, "viewer@alphacorp.com", PrincipalType.Human);
        var profile = await CreateLifecycleProfileAsync(tenant.Id, "agile", "Agile", "1.0");
        var project = await CreateProjectAsync(tenant.Id, "shop-api", profile.Id, owner.Id);

        var source = await CreateKnowledgeItemAsync(tenant.Id, project.Id, "requirement", owner.Id);
        var target = await CreateKnowledgeItemAsync(tenant.Id, project.Id, "design", owner.Id);

        using (var ownerClient = _factory.CreateAuthenticatedHumanClient(tenant.Id, owner.Id))
        {
            using var grant = await ownerClient.PostAsJsonAsync(
                $"/tenants/{tenant.Id}/projects/{project.Id}/members",
                new GrantProjectMembershipRequest(viewer.Id, ProjectRole.Viewer),
                JsonOptions);
            grant.EnsureSuccessStatusCode();
        }

        // Someone with no project membership at all cannot create a relationship in it.
        var outsiderAttempt = await TryCreateRelationshipAsync(
            tenant.Id, project.Id, source.Id, target.Id, "references", outsider.Id);
        Assert.Equal(HttpStatusCode.NotFound, outsiderAttempt.StatusCode);

        // A Viewer can see the project but cannot change its structure.
        var viewerAttempt = await TryCreateRelationshipAsync(
            tenant.Id, project.Id, source.Id, target.Id, "references", viewer.Id);
        Assert.Equal(HttpStatusCode.NotFound, viewerAttempt.StatusCode);

        var relationship = await CreateRelationshipAsync(tenant.Id, project.Id, source.Id, target.Id, "references", owner.Id);

        using (var viewerClient = _factory.CreateAuthenticatedHumanClient(tenant.Id, viewer.Id))
        {
            using var revokeAttempt = await viewerClient.PostAsJsonAsync(
                $"/tenants/{tenant.Id}/relationships/{relationship.Id}/revoke", new { }, JsonOptions);
            Assert.Equal(HttpStatusCode.NotFound, revokeAttempt.StatusCode);
        }

        using (var ownerClient = _factory.CreateAuthenticatedHumanClient(tenant.Id, owner.Id))
        {
            using var revoke = await ownerClient.PostAsJsonAsync(
                $"/tenants/{tenant.Id}/relationships/{relationship.Id}/revoke",
                new { reason = "No longer needed" }, JsonOptions);
            Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
        }
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

    private async Task<KnowledgeItemResponse> CreateKnowledgeItemAsync(Guid tenantId, Guid projectId, string key, Guid createdBy)
    {
        await SeedArchitectureSchemaAsync();

        using var client = _factory.CreateAuthenticatedHumanClient(tenantId, createdBy);
        using var response = await client.PostAsJsonAsync(
            $"/tenants/{tenantId}/projects/{projectId}/knowledge",
            new CreateKnowledgeItemRequest(
                key,
                "Architecture Overview",
                Classification.Public,
                RepresentationKind.Text,
                "architecture.mmd",
                "1.0",
                createdBy),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<KnowledgeItemResponse>(JsonOptions))!;
    }

    private async Task<RelationshipResponse> CreateRelationshipAsync(
        Guid tenantId,
        Guid projectId,
        Guid sourceItemId,
        Guid targetItemId,
        string relationTypeCode,
        Guid createdBy)
    {
        using var client = _factory.CreateAuthenticatedHumanClient(tenantId, createdBy);
        using var response = await client.PostAsJsonAsync(
            $"/tenants/{tenantId}/projects/{projectId}/relationships",
            new CreateRelationshipRequest(sourceItemId, targetItemId, relationTypeCode, createdBy),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RelationshipResponse>(JsonOptions))!;
    }

    private async Task<HttpResponseMessage> TryCreateRelationshipAsync(
        Guid tenantId,
        Guid projectId,
        Guid sourceItemId,
        Guid targetItemId,
        string relationTypeCode,
        Guid createdBy)
    {
        using var client = _factory.CreateAuthenticatedHumanClient(tenantId, createdBy);
        return await client.PostAsJsonAsync(
            $"/tenants/{tenantId}/projects/{projectId}/relationships",
            new CreateRelationshipRequest(sourceItemId, targetItemId, relationTypeCode, createdBy),
            JsonOptions);
    }

    private async Task<List<RelationTypeResponse>> ListRelationTypesAsync()
    {
        using var response = await _client.GetAsync("/relation-types");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<RelationTypeResponse>>(JsonOptions))!;
    }

    private async Task<List<RelationshipResponse>> ListRelationshipsForProjectAsync(Guid tenantId, Guid projectId)
    {
        using var response = await _client.GetAsync($"/tenants/{tenantId}/projects/{projectId}/relationships");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<RelationshipResponse>>(JsonOptions))!;
    }

    private async Task<RelationshipResponse?> GetRelationshipAsync(Guid tenantId, Guid relationshipId)
    {
        using var response = await _client.GetAsync($"/tenants/{tenantId}/relationships/{relationshipId}");
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RelationshipResponse>(JsonOptions);
    }

    private async Task<TraversalResponse> TraverseAsync(
        Guid tenantId,
        Guid itemId,
        TraversalDirection direction,
        int maxDepth,
        int maxResults)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/knowledge/{itemId}/relationships/traverse",
            new TraverseRelationshipsRequest(direction, maxDepth, maxResults),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TraversalResponse>(JsonOptions))!;
    }

    private async Task SeedArchitectureSchemaAsync()
    {
        using var listing = await _client.GetAsync("/schemas");
        listing.EnsureSuccessStatusCode();
        var schemas = await listing.Content.ReadFromJsonAsync<List<SchemaResponse>>(JsonOptions);

        var existing = schemas?.FirstOrDefault(s => s.Namespace == "architecture.mmd" && s.Version == "1.0");
        if (existing is not null)
        {
            if (existing.Status != SchemaStatus.Published)
            {
                using var publishExisting = await _client.PostAsync($"/schemas/{existing.Id}/publish", null);
                publishExisting.EnsureSuccessStatusCode();
            }

            return;
        }

        using var response = await _client.PostAsJsonAsync(
            "/schemas",
            new RegisterSchemaRequest("architecture.mmd", "1.0", "platform", "Integration test schema", "{\"type\":\"object\"}"),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        var created = (await response.Content.ReadFromJsonAsync<SchemaResponse>(JsonOptions))!;

        using var publish = await _client.PostAsync($"/schemas/{created.Id}/publish", null);
        publish.EnsureSuccessStatusCode();
    }
}
