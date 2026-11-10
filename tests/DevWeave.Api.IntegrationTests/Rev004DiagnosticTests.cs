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
using Xunit.Abstractions;

namespace DevWeave.Api.IntegrationTests;

public sealed class Rev004DiagnosticTests : IClassFixture<IdentityTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };

    private readonly IdentityTestFactory _factory;
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    public Rev004DiagnosticTests(IdentityTestFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _factory.PrepareDatabase();
        _client = _factory.CreateClient();
        _output = output;
    }

    [Fact]
    public async Task Reproduce_rev004_flow()
    {
        var tenant = await Post<TenantResponse>("/tenants", new CreateTenantRequest("Alpha Corp"));
        var principal = await Post<PrincipalResponse>(
            $"/tenants/{tenant.Id}/principals",
            new CreatePrincipalRequest("a@alphacorp.com", PrincipalType.Human));
        var profile = await Post<LifecycleProfileResponse>(
            $"/tenants/{tenant.Id}/lifecycle-profiles",
            new CreateLifecycleProfileRequest("agile", "Agile", "1.0"));
        var project = await Post<ProjectResponse>(
            $"/tenants/{tenant.Id}/projects",
            new CreateProjectRequest("Shop API", "shop-api", null, profile.Id, principal.Id));

        // The type must be an active (published) schema before an item can be created against it.
        var schema = await Post<SchemaResponse>(
            "/schemas",
            new RegisterSchemaRequest("architecture.mmd", "1.0", "platform", "Integration test schema", "{\"type\":\"object\"}"));
        _output.WriteLine($"SCHEMA_AFTER_REGISTER DOC=[{schema.Document}] STATUS={schema.Status}");

        using (var publish = await _client.PostAsync($"/schemas/{schema.Id}/publish", null))
        {
            publish.EnsureSuccessStatusCode();
            var pub = await publish.Content.ReadFromJsonAsync<SchemaResponse>(JsonOptions);
            _output.WriteLine($"SCHEMA_AFTER_PUBLISH DOC=[{pub!.Document}] STATUS={pub.Status}");
        }

        var item = await Post<KnowledgeItemResponse>(
            $"/tenants/{tenant.Id}/projects/{project.Id}/knowledge-items",
            new CreateKnowledgeItemRequest(
                "architecture", "Architecture Overview", Classification.Public,
                RepresentationKind.Text, "architecture.mmd", "1.0", principal.Id));

        using var rev = await _client.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/knowledge-items/{item.Id}/revisions",
            new AddRevisionRequest(0, "1.0", "{\"v\":1}", principal.Id, "Baseline", null, null),
            JsonOptions);
        var body = await rev.Content.ReadAsStringAsync();
        _output.WriteLine($"REVISION_STATUS={(int)rev.StatusCode}");
        _output.WriteLine($"REVISION_BODY={body}");
    }

    private async Task<T> Post<T>(string url, object payload)
    {
        using var response = await _client.PostAsJsonAsync(url, payload, JsonOptions);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            _output.WriteLine($"POST {url} -> {(int)response.StatusCode}: {body}");
        }
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }
}