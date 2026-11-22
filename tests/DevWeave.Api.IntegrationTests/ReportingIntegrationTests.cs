using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevWeave.Contracts.Planned.V1;
using DevWeave.Contracts.Identity.V1;
using DevWeave.Contracts.Knowledge.V1;
using DevWeave.Contracts.Projects.V1;
using DevWeave.Contracts.Lifecycle.V1;
using DevWeave.Contracts.Relations.V1;
using DevWeave.Contracts.Schema.V1;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Schema;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace DevWeave.Api.IntegrationTests;

public sealed class ReportingIntegrationTests : IClassFixture<IdentityTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };

    private readonly IdentityTestFactory _factory;
    private readonly HttpClient _client;

    public ReportingIntegrationTests(IdentityTestFactory factory)
    {
        _factory = factory;
        _factory.PrepareDatabase();
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task REP_001_002_project_reporting_metrics_and_generation_works()
    {
        // 1. Setup Tenant and Owner
        var tenant = await CreateTenantAsync("Reporting Corp");
        var owner = await CreateOwnerAsync(tenant.Id, "owner@reporting.test");
        var client = _factory.CreateAuthenticatedHumanClient(tenant.Id, owner.Id);

        // 2. Seed some data
        var profile = await CreateLifecycleProfileAsync(tenant.Id, "agile", "Agile", "1.0");
        var project = await CreateProjectAsync(tenant.Id, "report-project", profile.Id, owner.Id);
        
        await SeedPublishedSchemaAsync("architecture", "1.0", "{\"type\":\"object\"}");
        var item = await SeedKnowledgeItemAsync(tenant.Id, project.Id, "architecture", owner.Id);
        await AddRevisionAsync(tenant.Id, item.Id, owner.Id, 0, "{\"diagram\":\"layer-a\"}");

        // 3. Get Metrics (REP-001)
        var metricsResponse = await client.GetAsync($"/tenants/{tenant.Id}/projects/{project.Id}/metrics");
        Assert.Equal(HttpStatusCode.OK, metricsResponse.StatusCode);
        
        var metrics = (await metricsResponse.Content.ReadFromJsonAsync<List<MetricView>>(JsonOptions))!;
        Assert.NotEmpty(metrics);
        Assert.Contains(metrics, m => m.Key == "knowledge.total_items" && m.Value == 1);
        Assert.Contains(metrics, m => m.Key == "work.planned_count");
        Assert.Contains(metrics, m => m.Key == "work.reported_completed_count");
        Assert.Contains(metrics, m => m.Key == "work.verified_count");
        var estimate = Assert.Single(metrics, m => m.Key == "work.completion_estimate");
        Assert.True(estimate.IsEstimate);
        Assert.Equal("1.0", estimate.DefinitionVersion);

        // 4. Generate Report (REP-002)
        var reportDraft = new ReportDraft("Quarterly Review");
        var reportResponse = await client.PostAsJsonAsync($"/tenants/{tenant.Id}/projects/{project.Id}/reports", reportDraft, JsonOptions);
        Assert.Equal(HttpStatusCode.Accepted, reportResponse.StatusCode);
        
        var reportResult = await reportResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(reportResult.TryGetProperty("id", out _));

        // 5. Verify Audit Record for Report
        var auditResponse = await client.GetAsync($"/tenants/{tenant.Id}/audit");
        Assert.True(auditResponse.IsSuccessStatusCode, await auditResponse.Content.ReadAsStringAsync());
        var auditPage = (await auditResponse.Content.ReadFromJsonAsync<Page<AuditView>>(JsonOptions))!;
        Assert.Contains(auditPage.Items, a => a.Action == "ReportGenerated");
    }

    [Fact]
    public async Task Reporting_filters_hidden_items_and_edges_before_aggregation()
    {
        var tenant = await CreateTenantAsync("Reporting Safety Corp");
        var owner = await CreateOwnerAsync(tenant.Id, "owner@reporting-safety.test");
        var member = await CreatePrincipalAsync(tenant.Id, "member@reporting-safety.test");
        var viewer = await CreatePrincipalAsync(tenant.Id, "viewer@reporting-safety.test");
        var outsider = await CreatePrincipalAsync(tenant.Id, "outsider@reporting-safety.test");
        var profile = await CreateLifecycleProfileAsync(tenant.Id, "safe-agile", "Safe Agile", "1.0");
        var project = await CreateProjectAsync(tenant.Id, "safe-report-project", profile.Id, owner.Id);

        await ActivateTenantMembershipAsync(tenant.Id, member.Id, MembershipRole.Member);
        await ActivateTenantMembershipAsync(tenant.Id, viewer.Id, MembershipRole.Member);
        await ActivateTenantMembershipAsync(tenant.Id, outsider.Id, MembershipRole.Member);
        await GrantProjectMembershipAsync(tenant.Id, project.Id, member.Id, ProjectRole.Member);
        await GrantProjectMembershipAsync(tenant.Id, project.Id, viewer.Id, ProjectRole.Viewer);
        await SeedPublishedSchemaAsync("architecture", "1.0", "{\"type\":\"object\"}");

        var visible = await SeedKnowledgeItemAsync(
            tenant.Id, project.Id, "visible-architecture", owner.Id, Classification.Internal);
        var hidden = await SeedKnowledgeItemAsync(
            tenant.Id, project.Id, "restricted-architecture", owner.Id, Classification.Restricted);
        await CreateRelationshipAsync(tenant.Id, project.Id, visible.Id, hidden.Id, owner.Id);

        using var ownerClient = _factory.CreateAuthenticatedHumanClient(tenant.Id, owner.Id);
        using var memberClient = _factory.CreateAuthenticatedHumanClient(tenant.Id, member.Id);
        using var viewerClient = _factory.CreateAuthenticatedHumanClient(tenant.Id, viewer.Id);
        using var outsiderClient = _factory.CreateAuthenticatedHumanClient(tenant.Id, outsider.Id);

        var ownerMetrics = await GetMetricsAsync(ownerClient, tenant.Id, project.Id);
        AssertMetric(ownerMetrics, "knowledge.total_items", 2);
        AssertMetric(ownerMetrics, "knowledge.relationship_count", 1);

        foreach (var restrictedClient in new[] { memberClient, viewerClient })
        {
            var metrics = await GetMetricsAsync(restrictedClient, tenant.Id, project.Id);
            AssertMetric(metrics, "knowledge.total_items", 1);
            var relationshipMetric = AssertMetric(metrics, "knowledge.relationship_count", 0);
            Assert.DoesNotContain("1/", relationshipMetric.IncludedPopulation, StringComparison.Ordinal);
            Assert.DoesNotContain("1 relationship", relationshipMetric.ExcludedPopulation, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("hidden cardinality is not disclosed", relationshipMetric.ExcludedPopulation);
        }

        using var metricAttempt = await outsiderClient.GetAsync(
            $"/tenants/{tenant.Id}/projects/{project.Id}/metrics");
        Assert.Equal(HttpStatusCode.NotFound, metricAttempt.StatusCode);

        using var reportAttempt = await outsiderClient.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/projects/{project.Id}/reports",
            new ReportDraft("Unauthorized report"),
            JsonOptions);
        Assert.Equal(HttpStatusCode.NotFound, reportAttempt.StatusCode);
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
            new CreateProjectRequest("Reporting Project", key, null, lifecycleProfileId, createdBy),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        // CreateProject grants the creator Owner project membership.
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

    private static async Task<List<MetricView>> GetMetricsAsync(
        HttpClient client,
        Guid tenantId,
        Guid projectId)
    {
        using var response = await client.GetAsync($"/tenants/{tenantId}/projects/{projectId}/metrics");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<MetricView>>(JsonOptions))!;
    }

    private static MetricView AssertMetric(
        IEnumerable<MetricView> metrics,
        string key,
        decimal expectedValue)
    {
        var metric = Assert.Single(metrics, candidate => candidate.Key == key);
        Assert.Equal(expectedValue, metric.Value);
        return metric;
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
