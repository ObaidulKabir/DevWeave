using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevWeave.Contracts.Identity.V1;
using DevWeave.Contracts.Knowledge.V1;
using DevWeave.Contracts.Lifecycle.V1;
using DevWeave.Contracts.Projects.V1;
using DevWeave.Contracts.Schema.V1;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Api.IntegrationTests;

public sealed class CanonicalCatalogueIntegrationTests : IClassFixture<IdentityTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };

    private readonly IdentityTestFactory _factory;
    private readonly HttpClient _client;

    public CanonicalCatalogueIntegrationTests(IdentityTestFactory factory)
    {
        _factory = factory;
        _factory.PrepareDatabase();
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Fresh_database_seeds_published_catalogue_and_validates_nfr_payloads()
    {
        using var typesResponse = await _client.GetAsync("/schemas/types");
        Assert.Equal(HttpStatusCode.OK, typesResponse.StatusCode);
        var types = (await typesResponse.Content.ReadFromJsonAsync<List<KnowledgeTypeMetadataResponse>>(JsonOptions))!;
        Assert.Contains(types, type => type.TypeId == "requirements.functional" && type.IsCanonical);
        Assert.Contains(types, type => type.TypeId == "requirements.nonfunctional" && type.IsCanonical);
        Assert.Contains(types, type => type.TypeId == "planning.work-item" && type.StorageKind == "WorkItemAggregate");

        using var schemasResponse = await _client.GetAsync("/schemas");
        Assert.Equal(HttpStatusCode.OK, schemasResponse.StatusCode);
        var schemas = (await schemasResponse.Content.ReadFromJsonAsync<List<SchemaResponse>>(JsonOptions))!;
        Assert.Contains(
            schemas,
            schema => schema.Namespace == "requirements.nonfunctional" &&
                      schema.Status == Domain.Schema.SchemaStatus.Published &&
                      schema.Type is { IsCanonical: true });

        var tenant = await CreateTenantAsync("Catalogue Corp");
        var owner = await CreateOwnerAsync(tenant.Id, "owner@catalogue.test");
        var profile = await CreateLifecycleProfileAsync(tenant.Id, "catalogue-agile", "Catalogue Agile", "1.0");
        var project = await CreateProjectAsync(tenant.Id, "catalogue-project", profile.Id, owner.Id);
        using var client = _factory.CreateAuthenticatedHumanClient(tenant.Id, owner.Id);

        using var createItem = await client.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/projects/{project.Id}/knowledge",
            new CreateKnowledgeItemRequest(
                "nfr-latency",
                "Checkout latency",
                Classification.Internal,
                RepresentationKind.StructuredData,
                "requirements.nonfunctional",
                "1.0",
                owner.Id),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Created, createItem.StatusCode);
        var item = (await createItem.Content.ReadFromJsonAsync<KnowledgeItemResponse>(JsonOptions))!;

        using var invalidRevision = await client.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/knowledge/{item.Id}/revisions",
            new AddRevisionRequest(0, "1.0", """{"statement":"too incomplete"}""", owner.Id, "Draft NFR", null, null),
            JsonOptions);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalidRevision.StatusCode);

        var validPayload = """
            {
              "statement": "p95 checkout latency stays under 300ms",
              "qualityAttribute": "performance",
              "metric": { "name": "checkout_latency_p95", "aggregation": "percentile_95", "unit": "ms" },
              "target": { "operator": "lte", "value": 300 },
              "condition": { "workload": "100 rps steady", "environment": "staging" },
              "verificationMethod": "load-test"
            }
            """;
        using var validRevision = await client.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/knowledge/{item.Id}/revisions",
            new AddRevisionRequest(0, "1.0", validPayload, owner.Id, "Measurable NFR", null, null),
            JsonOptions);
        Assert.True(validRevision.IsSuccessStatusCode, await validRevision.Content.ReadAsStringAsync());

        using var aggregateAttempt = await client.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/projects/{project.Id}/knowledge",
            new CreateKnowledgeItemRequest(
                "fake-work-item",
                "Should fail",
                Classification.Internal,
                RepresentationKind.StructuredData,
                "planning.work-item",
                "1.0",
                owner.Id),
            JsonOptions);
        Assert.Equal(HttpStatusCode.NotFound, aggregateAttempt.StatusCode);
    }

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
            new CreateProjectRequest("Catalogue Project", key, null, lifecycleProfileId, createdBy),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions))!;
    }
}
