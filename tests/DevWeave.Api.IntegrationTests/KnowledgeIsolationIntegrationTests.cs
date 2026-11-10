using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevWeave.Application.Abstractions;
using DevWeave.Contracts.Identity.V1;
using DevWeave.Contracts.Knowledge.V1;
using DevWeave.Contracts.Lifecycle.V1;
using DevWeave.Contracts.Projects.V1;
using DevWeave.Contracts.Schema.V1;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Schema;
using Microsoft.Extensions.DependencyInjection;

namespace DevWeave.Api.IntegrationTests;

/// <summary>
/// Proves the knowledge isolation invariants (KNW-001/KNW-002/KNW-003/KNW-013, REV-001/REV-004,
/// BR-004) end-to-end against a real database: a query scoped to tenant A must never disclose tenant
/// B knowledge items or revisions across every read path (deny-by-default), a duplicate key conflicts
/// with 409, and a stale revision write conflicts with 409.
/// </summary>
public sealed class KnowledgeIsolationIntegrationTests : IClassFixture<IdentityTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };

    private readonly IdentityTestFactory _factory;
    private readonly HttpClient _client;

    public KnowledgeIsolationIntegrationTests(IdentityTestFactory factory)
    {
        _factory = factory;
        _factory.PrepareDatabase();
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task KNW_001_002_003_knowledge_item_and_revision_reads_never_disclose_cross_tenant_data()
    {
        var alpha = await CreateTenantAsync("Alpha Corp");
        var beta = await CreateTenantAsync("Beta Corp");
        await SeedPublishedSchemaAsync("architecture.mmd", "1.0", "{\"type\":\"object\"}");

        var principalA = await CreatePrincipalAsync(alpha.Id, "a@alphacorp.com", PrincipalType.Human);
        var principalB = await CreatePrincipalAsync(beta.Id, "b@betacorp.com", PrincipalType.Human);

        var profileA = await CreateLifecycleProfileAsync(alpha.Id, "agile", "Agile", "1.0");
        var profileB = await CreateLifecycleProfileAsync(beta.Id, "agile", "Agile", "1.0");

        var projectA = await CreateProjectAsync(alpha.Id, "shop-api", profileA.Id, principalA.Id);
        var projectB = await CreateProjectAsync(beta.Id, "shop-api", profileB.Id, principalB.Id);

        var itemA = await CreateKnowledgeItemAsync(alpha.Id, projectA.Id, "architecture", principalA.Id);
        var itemB = await CreateKnowledgeItemAsync(beta.Id, projectB.Id, "architecture", principalB.Id);

        var revisionA = await AddRevisionAsync(alpha.Id, itemA.Id, principalA.Id, 0, "{\"diagram\":\"layer-a\"}");
        var revisionB = await AddRevisionAsync(beta.Id, itemB.Id, principalB.Id, 0, "{\"diagram\":\"layer-b\"}");

        // HTTP tenant read path
        var alphaItems = await ListKnowledgeItemsForTenantAsync(alpha.Id);
        Assert.Single(alphaItems);
        Assert.Equal(itemA.Id, alphaItems[0].Id);
        Assert.DoesNotContain(alphaItems, i => i.Id == itemB.Id);

        var crossTenantItem = await GetKnowledgeItemAsync(alpha.Id, itemB.Id);
        Assert.Null(crossTenantItem);

        // HTTP project read path
        var alphaProjectItems = await ListKnowledgeItemsForProjectAsync(alpha.Id, projectA.Id);
        Assert.Single(alphaProjectItems);
        Assert.Equal(itemA.Id, alphaProjectItems[0].Id);

        var crossTenantProjectItems = await ListKnowledgeItemsForProjectAsync(alpha.Id, projectB.Id);
        Assert.Empty(crossTenantProjectItems);

        // HTTP revision read path
        var crossTenantRevision = await GetRevisionAsync(alpha.Id, revisionB.Id);
        Assert.Null(crossTenantRevision);

        // Repository read paths (negative disclosure across every read method)
        using var scope = _factory.Services.CreateScope();
        var items = scope.ServiceProvider.GetRequiredService<IKnowledgeItemRepository>();
        var revisions = scope.ServiceProvider.GetRequiredService<IKnowledgeRevisionRepository>();

        var scopedItems = await items.ListByTenantAsync(new TenantId(alpha.Id));
        Assert.Single(scopedItems);
        Assert.Equal(itemA.Id, scopedItems[0].Id.Value);
        Assert.DoesNotContain(scopedItems, i => i.Id.Value == itemB.Id);

        Assert.Null(await items.GetByIdAsync(new TenantId(alpha.Id), new KnowledgeItemId(itemB.Id)));

        var scopedProjectItems = await items.ListByProjectAsync(new TenantId(alpha.Id), new ProjectId(projectA.Id));
        Assert.Single(scopedProjectItems);
        Assert.DoesNotContain(scopedProjectItems, i => i.Id.Value == itemB.Id);
        Assert.Empty(await items.ListByProjectAsync(new TenantId(alpha.Id), new ProjectId(projectB.Id)));

        Assert.Null(await revisions.GetByIdAsync(new TenantId(alpha.Id), new KnowledgeRevisionId(revisionB.Id)));
        Assert.Empty(await revisions.ListByItemAsync(new TenantId(alpha.Id), new KnowledgeItemId(itemB.Id)));
    }

    [Fact]
    public async Task REV_004_stale_revision_write_and_duplicate_key_return_409_conflict()
    {
        var tenant = await CreateTenantAsync("Alpha Corp");
        var principal = await CreatePrincipalAsync(tenant.Id, "a@alphacorp.com", PrincipalType.Human);
        var profile = await CreateLifecycleProfileAsync(tenant.Id, "agile", "Agile", "1.0");
        var project = await CreateProjectAsync(tenant.Id, "shop-api", profile.Id, principal.Id);

        await SeedPublishedSchemaAsync("architecture.mmd", "1.0", "{\"type\":\"object\"}");

        var item = await CreateKnowledgeItemAsync(tenant.Id, project.Id, "architecture", principal.Id);

        // First revision advances the item to revision 1.
        var first = await AddRevisionAsync(tenant.Id, item.Id, principal.Id, 0, "{\"v\":1}");
        Assert.Equal(1, first.RevisionNumber);

        // A stale write (still expecting revision 0) must conflict, not silently overwrite.
        var stale = await TryAddRevisionAsync(tenant.Id, item.Id, principal.Id, 0, "{\"v\":2}");
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var staleBody = (await stale.Content.ReadFromJsonAsync<KnowledgeConflict>(JsonOptions))!;
        Assert.Equal("stale-revision", staleBody.Code);
        Assert.NotNull(staleBody.Message);

        // The item still points at revision 1: the stale write changed nothing.
        var fetched = await GetKnowledgeItemAsync(tenant.Id, item.Id);
        Assert.Equal(1, fetched!.CurrentRevisionNumber);

        // A duplicate key within the same project conflicts (BR-004 / invariant #2).
        var duplicate = await TryCreateKnowledgeItemAsync(tenant.Id, project.Id, "architecture", principal.Id);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var duplicateBody = (await duplicate.Content.ReadFromJsonAsync<KnowledgeConflict>(JsonOptions))!;
        Assert.Equal("duplicate-key", duplicateBody.Code);

        // A revision whose schema version disagrees with the item's pinned version is rejected with a
        // distinct code (never confused with a stale-revision conflict).
        var mismatch = await TryAddRevisionAsync(tenant.Id, item.Id, principal.Id, 1, "{\"v\":3}", schemaVersion: "2.0");
        Assert.Equal(HttpStatusCode.Conflict, mismatch.StatusCode);
        var mismatchBody = (await mismatch.Content.ReadFromJsonAsync<KnowledgeConflict>(JsonOptions))!;
        Assert.Equal("schema-version-mismatch", mismatchBody.Code);
    }

    [Fact]
    public async Task ART_008_AC_09_prohibited_revision_content_is_refused_across_the_api()
    {
        var tenant = await CreateTenantAsync("Alpha Corp");
        var principal = await CreatePrincipalAsync(tenant.Id, "a@alphacorp.com", PrincipalType.Human);
        var profile = await CreateLifecycleProfileAsync(tenant.Id, "agile", "Agile", "1.0");
        var project = await CreateProjectAsync(tenant.Id, "shop-api", profile.Id, principal.Id);

        await SeedPublishedSchemaAsync("architecture.mmd", "1.0", "{\"type\":\"object\"}");
        var item = await CreateKnowledgeItemAsync(tenant.Id, project.Id, "architecture", principal.Id);

        // The shared content policy runs on knowledge ingestion too, so secret-bearing content is
        // rejected before anything is persisted (SRS 7.7, ART-008, AC-09).
        var secret = await TryAddRevisionAsync(
            tenant.Id, item.Id, principal.Id, 0, "{\"token\":\"AKIAABCDEFGHIJKLMNOP\"}");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, secret.StatusCode);
        var secretBody = (await secret.Content.ReadFromJsonAsync<KnowledgeConflict>(JsonOptions))!;
        Assert.Equal("prohibited-content", secretBody.Code);

        // Source code mislabelled as structured data is caught by content inspection, not the enum guard.
        var code = await TryAddRevisionAsync(
            tenant.Id, item.Id, principal.Id, 0,
            "using System; public class Program { public static void Main() { } }");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, code.StatusCode);
        var codeBody = (await code.Content.ReadFromJsonAsync<KnowledgeConflict>(JsonOptions))!;
        Assert.Equal("prohibited-content", codeBody.Code);

        // Nothing was persisted: the item remains at revision 0.
        var fetched = await GetKnowledgeItemAsync(tenant.Id, item.Id);
        Assert.Equal(0, fetched!.CurrentRevisionNumber);
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

    private async Task<HttpResponseMessage> TryCreateKnowledgeItemAsync(Guid tenantId, Guid projectId, string key, Guid createdBy)
    {
        return await _client.PostAsJsonAsync(
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
    }

    private async Task<List<KnowledgeItemResponse>> ListKnowledgeItemsForTenantAsync(Guid tenantId)
    {
        using var response = await _client.GetAsync($"/tenants/{tenantId}/knowledge-items");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<KnowledgeItemResponse>>(JsonOptions))!;
    }

    private async Task<List<KnowledgeItemResponse>> ListKnowledgeItemsForProjectAsync(Guid tenantId, Guid projectId)
    {
        using var response = await _client.GetAsync($"/tenants/{tenantId}/projects/{projectId}/knowledge-items");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<KnowledgeItemResponse>>(JsonOptions))!;
    }

    private async Task<KnowledgeItemResponse?> GetKnowledgeItemAsync(Guid tenantId, Guid itemId)
    {
        using var response = await _client.GetAsync($"/tenants/{tenantId}/knowledge-items/{itemId}");
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<KnowledgeItemResponse>(JsonOptions);
    }

    private async Task<KnowledgeRevisionResponse> AddRevisionAsync(Guid tenantId, Guid itemId, Guid actor, int expected, string content)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/knowledge-items/{itemId}/revisions",
            new AddRevisionRequest(expected, "1.0", content, actor, "Baseline", null, null),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<KnowledgeRevisionResponse>(JsonOptions))!;
    }

    private async Task<HttpResponseMessage> TryAddRevisionAsync(
        Guid tenantId,
        Guid itemId,
        Guid actor,
        int expected,
        string content,
        string schemaVersion = "1.0")
    {
        return await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/knowledge-items/{itemId}/revisions",
            new AddRevisionRequest(expected, schemaVersion, content, actor, "Baseline", null, null),
            JsonOptions);
    }

    private async Task<KnowledgeRevisionResponse?> GetRevisionAsync(Guid tenantId, Guid revisionId)
    {
        using var response = await _client.GetAsync($"/tenants/{tenantId}/revisions/{revisionId}");
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<KnowledgeRevisionResponse>(JsonOptions);
    }

    private async Task<Guid> SeedPublishedSchemaAsync(string @namespace, string version, string document)
    {
        var existing = await FindSchemaAsync(@namespace, version);
        if (existing is not null)
        {
            if (existing.Status != SchemaStatus.Published)
            {
                await PublishSchemaAsync(existing.Id);
            }

            return existing.Id;
        }

        var created = await RegisterSchemaAsync(@namespace, version, document);
        await PublishSchemaAsync(created.Id);
        return created.Id;
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