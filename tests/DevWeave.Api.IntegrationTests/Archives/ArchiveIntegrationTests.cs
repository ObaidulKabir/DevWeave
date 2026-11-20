using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevWeave.Contracts.Planned.V1;
using DevWeave.Contracts.Identity.V1;
using DevWeave.Contracts.Knowledge.V1;
using DevWeave.Contracts.Projects.V1;
using DevWeave.Contracts.Lifecycle.V1;
using DevWeave.Contracts.Schema.V1;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Schema;
using Microsoft.Extensions.DependencyInjection;
using DevWeave.Application.Abstractions;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Api.IntegrationTests.Archives;

public sealed class ArchiveIntegrationTests : IClassFixture<IdentityTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };

    private readonly IdentityTestFactory _factory;
    private readonly HttpClient _client;

    public ArchiveIntegrationTests(IdentityTestFactory factory)
    {
        _factory = factory;
        _factory.PrepareDatabase();
        _client = _factory.CreateClient();
    }

    private async Task<KnowledgeRevisionResponse> AddRevisionAsync(Guid tenantId, Guid itemId, Guid actor, int expected, string content)
    {
        using var client = _factory.CreateAuthenticatedHumanClient(tenantId, actor);
        using var response = await client.PostAsJsonAsync(
            $"/tenants/{tenantId}/knowledge/{itemId}/revisions",
            new AddRevisionRequest(expected, "1.0", content, actor, "Baseline", null, null),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<KnowledgeRevisionResponse>(JsonOptions))!;
    }

    [Fact]
    public async Task DATA_01_002_003_export_lifecycle_works_end_to_end()
    {
        // 1. Setup Tenant and Owner
        var tenant = await CreateTenantAsync("Alpha Corp");
        var owner = await CreateOwnerAsync(tenant.Id, "owner@alphacorp.com");
        var client = _factory.CreateAuthenticatedHumanClient(tenant.Id, owner.Id);

        // 2. Seed some data (Project and Knowledge)
        var profile = await CreateLifecycleProfileAsync(tenant.Id, "agile", "Agile", "1.0");
        var project = await CreateProjectAsync(tenant.Id, "shop-api", profile.Id, owner.Id);
        
        await SeedPublishedSchemaAsync("architecture", "1.0", "{\"type\":\"object\"}");
        var item = await SeedKnowledgeItemAsync(tenant.Id, project.Id, "architecture", owner.Id);
        await AddRevisionAsync(tenant.Id, item.Id, owner.Id, 0, "{\"diagram\":\"layer-a\"}");

        // 3. Describe Export (DATA-01)
        var describeResponse = await client.GetAsync($"/tenants/{tenant.Id}/export/manifest");
        Assert.Equal(HttpStatusCode.OK, describeResponse.StatusCode);
        
        var manifest = (await describeResponse.Content.ReadFromJsonAsync<ExportManifest>(JsonOptions))!;
        Assert.Equal(tenant.Id, manifest.TenantId);
        Assert.Single(manifest.Revisions);
        Assert.Equal(item.Id, manifest.Revisions[0].Id);

        // 4. Begin Export (DATA-03)
        var exportDraft = new ExportDraft("export-idem-1");
        var exportResponse = await client.PostAsJsonAsync($"/tenants/{tenant.Id}/export", exportDraft, JsonOptions);
        Assert.Equal(HttpStatusCode.Accepted, exportResponse.StatusCode);

        // 5. Begin Import (DATA-05)
        var importDraft = new ImportDraft("manifest-ref-1", "import-idem-1");
        var importResponse = await client.PostAsJsonAsync($"/tenants/{tenant.Id}/import", importDraft, JsonOptions);
        Assert.Equal(HttpStatusCode.Accepted, importResponse.StatusCode);
    }

    [Fact]
    public async Task COM_020_export_remains_available_without_a_commercial_assignment()
    {
        var tenant = await CreateTenantAsync("Exit Tenant");
        var owner = await CreateOwnerAsync(tenant.Id, "exit-owner@test.com");
        var client = _factory.CreateAuthenticatedHumanClient(tenant.Id, owner.Id);

        // No commercial plan, managed runtime, or execution provider is configured for this tenant.
        using var manifestResponse = await client.GetAsync($"/tenants/{tenant.Id}/export/manifest");
        Assert.Equal(HttpStatusCode.OK, manifestResponse.StatusCode);

        using var exportResponse = await client.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/export",
            new ExportDraft($"exit-{Guid.NewGuid():N}"),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Accepted, exportResponse.StatusCode);
    }

    [Fact]
    public async Task EXP_001_002_manifest_checksums_match_stored_artifacts_and_import_rejects_unknown_checksums()
    {
        var tenant = await CreateTenantAsync("Export Integrity Co");
        var owner = await CreateOwnerAsync(tenant.Id, "owner@export.test");
        var client = _factory.CreateAuthenticatedHumanClient(tenant.Id, owner.Id);
        var profile = await CreateLifecycleProfileAsync(tenant.Id, "agile", "Agile", "1.0");
        var project = await CreateProjectAsync(tenant.Id, "export-project", profile.Id, owner.Id);

        using var form = new MultipartFormDataContent();
        var bytes = System.Text.Encoding.UTF8.GetBytes("exportable evidence bytes");
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", "notes.txt");
        form.Add(new StringContent("notes.txt"), "key");
        form.Add(new StringContent("notes.txt"), "fileName");
        form.Add(new StringContent("text/plain"), "mediaType");
        form.Add(new StringContent("Internal"), "classification");
        form.Add(new StringContent(owner.Id.ToString()), "creator");

        using var upload = await client.PostAsync($"/tenants/{tenant.Id}/projects/{project.Id}/artifacts", form);
        upload.EnsureSuccessStatusCode();
        using var artifactDoc = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());
        var checksum = artifactDoc.RootElement.GetProperty("checksum").GetString();
        Assert.False(string.IsNullOrWhiteSpace(checksum));

        using var manifestResponse = await client.GetAsync($"/tenants/{tenant.Id}/export/manifest");
        manifestResponse.EnsureSuccessStatusCode();
        var manifest = (await manifestResponse.Content.ReadFromJsonAsync<ExportManifest>(JsonOptions))!;
        Assert.Contains(checksum!, manifest.ArtifactChecksums);

        using var goodImport = await client.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/import",
            new ImportDraft($"checksums:{checksum}", $"import-ok-{Guid.NewGuid():N}"),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Accepted, goodImport.StatusCode);

        using var badImport = await client.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/import",
            new ImportDraft("checksums:sha256:not-present-in-tenant", $"import-bad-{Guid.NewGuid():N}"),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, badImport.StatusCode);
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
            new CreateProjectRequest("Shop API", key, null, lifecycleProfileId, createdBy),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        // CreateProject grants the creator Owner project membership.
        return (await response.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions))!;
    }

    private async Task<KnowledgeItemResponse> SeedKnowledgeItemAsync(Guid tenantId, Guid projectId, string key, Guid createdBy)
    {
        var client = _factory.CreateAuthenticatedHumanClient(tenantId, createdBy);
        var draft = new CreateKnowledgeItemRequest(
            key, "Title", Classification.Internal, RepresentationKind.Text, "architecture", "1.0", createdBy);
        
        using var response = await client.PostAsJsonAsync($"/tenants/{tenantId}/projects/{projectId}/knowledge", draft, JsonOptions);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new Exception($"404 Not Found at /tenants/{tenantId}/projects/{projectId}/knowledge. Response: {content}");
        }
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<KnowledgeItemResponse>(JsonOptions))!;
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
