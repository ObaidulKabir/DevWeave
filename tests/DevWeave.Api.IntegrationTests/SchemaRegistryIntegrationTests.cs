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
using DevWeave.Domain.Schema;

namespace DevWeave.Api.IntegrationTests;

/// <summary>
/// Proves the schema registry surface (SRS SCH-001, SCH-003, KNW-002/KNW-004, KNW-008) end-to-end
/// against a real database and the real JsonSchema.Net-backed validator: a duplicate (Namespace,
/// Version) conflicts with 409, publishing makes a schema discoverable, an invalid typed revision
/// returns 422 with path-specific errors, and a revision against an unknown schema returns 404.
/// </summary>
public sealed class SchemaRegistryIntegrationTests : IClassFixture<IdentityTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };

    private readonly IdentityTestFactory _factory;
    private readonly HttpClient _client;

    public SchemaRegistryIntegrationTests(IdentityTestFactory factory)
    {
        _factory = factory;
        _factory.PrepareDatabase();
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task SCH_003_publishing_a_schema_makes_it_discoverable()
    {
        var schema = await RegisterSchemaAsync("core.resource", "1.0", "{\"type\":\"object\"}");
        Assert.Equal(SchemaStatus.Draft, schema.Status);

        var published = await PublishSchemaAsync(schema.Id);
        Assert.Equal(SchemaStatus.Published, published.Status);

        var discovered = await DiscoverSchemasAsync();
        Assert.Contains(discovered, d => d.Namespace == "core.resource" && d.Version == "1.0");
    }

    [Fact]
    public async Task SCH_001_registering_a_duplicate_namespace_and_version_conflicts_with_409()
    {
        await RegisterSchemaAsync("core.resource.dup", "1.0", "{\"type\":\"object\"}");

        var duplicate = await TryRegisterSchemaAsync("core.resource.dup", "1.0", "{\"type\":\"object\"}");

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var body = (await duplicate.Content.ReadFromJsonAsync<SchemaConflict>(JsonOptions))!;
        Assert.Equal("duplicate-schema", body.Code);
        Assert.NotNull(body.Message);
    }

    [Fact]
    public async Task KNW_004_an_invalid_revision_payload_returns_422_with_path_errors()
    {
        await RegisterAndPublishSchemaAsync(
            "architecture.mmd",
            "1.0",
            "{\"type\":\"object\",\"required\":[\"diagram\"],\"properties\":{\"diagram\":{\"type\":\"string\"}}}");

        var tenant = await CreateTenantAsync("Alpha Corp");
        var principal = await CreatePrincipalAsync(tenant.Id, "a@alphacorp.com", PrincipalType.Human);
        var profile = await CreateLifecycleProfileAsync(tenant.Id, "agile", "Agile", "1.0");
        var project = await CreateProjectAsync(tenant.Id, "shop-api", profile.Id, principal.Id);
        var item = await CreateKnowledgeItemAsync(tenant.Id, project.Id, "architecture", principal.Id);

        var response = await TryAddRevisionAsync(tenant.Id, item.Id, principal.Id, 0, "{\"diagram\":123}");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var body = (await response.Content.ReadFromJsonAsync<SchemaValidationErrorResponse>(JsonOptions))!;
        Assert.NotEmpty(body.Errors);
        Assert.Contains(body.Errors, e => e.Path == "/diagram");
    }

    [Fact]
    public async Task KNW_002_creating_an_item_against_an_unknown_schema_returns_404()
    {
        var tenant = await CreateTenantAsync("Alpha Corp");
        var principal = await CreatePrincipalAsync(tenant.Id, "a@alphacorp.com", PrincipalType.Human);
        var profile = await CreateLifecycleProfileAsync(tenant.Id, "agile", "Agile", "1.0");
        var project = await CreateProjectAsync(tenant.Id, "shop-api", profile.Id, principal.Id);

        using var response = await _client.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/projects/{project.Id}/knowledge-items",
            new CreateKnowledgeItemRequest(
                "architecture",
                "Architecture Overview",
                Classification.Public,
                RepresentationKind.Text,
                "architecture.unknown",
                "1.0",
                principal.Id),
            JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
        using var response = await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/projects",
            new CreateProjectRequest("Shop API", key, null, lifecycleProfileId, createdBy),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions))!;
    }

    private async Task<KnowledgeItemResponse> CreateKnowledgeItemAsync(Guid tenantId, Guid projectId, string key, Guid createdBy)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/projects/{projectId}/knowledge-items",
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

    private async Task<SchemaResponse> RegisterSchemaAsync(string @namespace, string version, string document)
    {
        using var response = await _client.PostAsJsonAsync(
            "/schemas",
            new RegisterSchemaRequest(@namespace, version, "platform", "Integration test schema", document),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SchemaResponse>(JsonOptions))!;
    }

    private async Task<HttpResponseMessage> TryRegisterSchemaAsync(string @namespace, string version, string document)
    {
        return await _client.PostAsJsonAsync(
            "/schemas",
            new RegisterSchemaRequest(@namespace, version, "platform", "Integration test schema", document),
            JsonOptions);
    }

    private async Task RegisterAndPublishSchemaAsync(string @namespace, string version, string document)
    {
        var schema = await RegisterSchemaAsync(@namespace, version, document);
        await PublishSchemaAsync(schema.Id);
    }

    private async Task<SchemaResponse> PublishSchemaAsync(Guid id)
    {
        using var response = await _client.PostAsync($"/schemas/{id}/publish", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SchemaResponse>(JsonOptions))!;
    }

    private async Task<List<SchemaDiscoveryItemResponse>> DiscoverSchemasAsync()
    {
        using var response = await _client.GetAsync("/schemas/discover");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<SchemaDiscoveryItemResponse>>(JsonOptions))!;
    }

    private async Task<HttpResponseMessage> TryAddRevisionAsync(Guid tenantId, Guid itemId, Guid actor, int expected, string content)
    {
        return await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/knowledge-items/{itemId}/revisions",
            new AddRevisionRequest(expected, "1.0", content, actor, "Baseline", null, null),
            JsonOptions);
    }
}