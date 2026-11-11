using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevWeave.Application.Abstractions;
using DevWeave.Contracts.Activities.V1;
using DevWeave.Contracts.Identity.V1;
using DevWeave.Contracts.Knowledge.V1;
using DevWeave.Contracts.Lifecycle.V1;
using DevWeave.Contracts.Projects.V1;
using DevWeave.Contracts.Schema.V1;
using DevWeave.Domain.Activities;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Schema;
using Microsoft.Extensions.DependencyInjection;

namespace DevWeave.Api.IntegrationTests.Activities;

/// <summary>
/// Proves the provenance surface (SRS PRO-001..PRO-005, PRO-008, REV-006..REV-008) end-to-end against
/// a real database: an activity records and reads back its revision-pinned inputs/outputs, the surface
/// is append-only (no update/delete), the REV-008 amendment guard surfaces as HTTP 409, every read path
/// never discloses a cross-tenant activity, state reconstruction rolls an item back to its as-of
/// revision, and the PROV-O export projects the actor/used/wasGeneratedBy shape.
/// </summary>
public sealed class ActivityIntegrationTests : IClassFixture<IdentityTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };

    private readonly IdentityTestFactory _factory;
    private readonly HttpClient _client;

    public ActivityIntegrationTests(IdentityTestFactory factory)
    {
        _factory = factory;
        _factory.PrepareDatabase();
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task PRO_001_002_003_004_record_then_list_round_trips_revision_pinned_inputs_and_outputs()
    {
        var tenant = await CreateTenantAsync("Alpha Corp");
        var principal = await CreatePrincipalAsync(tenant.Id, "a@alphacorp.com", PrincipalType.Human);
        var profile = await CreateLifecycleProfileAsync(tenant.Id, "agile", "Agile", "1.0");
        var project = await CreateProjectAsync(tenant.Id, "shop-api", profile.Id, principal.Id);

        var item = await CreateKnowledgeItemAsync(tenant.Id, project.Id, "architecture", principal.Id);
        var revision = await AddRevisionAsync(tenant.Id, item.Id, principal.Id, 0, "{\"v\":1}");

        var start = DateTimeOffset.UtcNow;
        var activity = await RecordActivityAsync(
            tenant.Id,
            project.Id,
            principal.Id,
            ActivityType.Generated,
            "generate first architecture revision",
            start,
            endedAt: start.AddSeconds(1),
            inputs: new[] { new ActivityInputRequest(item.Id, revision.Id, 0) },
            outputs: new[] { new ActivityOutputRequest(item.Id, revision.Id, 0) });

        Assert.NotEqual(Guid.Empty, activity.Id);
        Assert.Equal(tenant.Id, activity.TenantId);
        Assert.Equal(project.Id, activity.ProjectId);
        Assert.Equal(ActivityType.Generated, activity.Type);
        Assert.Equal(principal.Id, activity.Actor);

        var input = Assert.Single(activity.Inputs);
        Assert.Equal(item.Id, input.ItemId);
        Assert.Equal(revision.Id, input.RevisionId);
        Assert.Equal(0, input.Position);

        var output = Assert.Single(activity.Outputs);
        Assert.Equal(item.Id, output.ItemId);
        Assert.Equal(revision.Id, output.RevisionId);
        Assert.Equal(0, output.Position);

        // List-for-project and get-by-id return the same immutable record.
        var listed = await ListActivitiesForProjectAsync(tenant.Id, project.Id);
        var fromList = Assert.Single(listed);
        Assert.Equal(activity.Id, fromList.Id);

        var fetched = await GetActivityAsync(tenant.Id, activity.Id);
        Assert.Equal(activity.Id, fetched!.Id);
        Assert.Equal(revision.Id, Assert.Single(fetched.Outputs).RevisionId);
    }

    [Fact]
    public async Task REV_006_the_activity_surface_is_append_only()
    {
        var tenant = await CreateTenantAsync("Alpha Corp");
        var principal = await CreatePrincipalAsync(tenant.Id, "a@alphacorp.com", PrincipalType.Human);
        var profile = await CreateLifecycleProfileAsync(tenant.Id, "agile", "Agile", "1.0");
        var project = await CreateProjectAsync(tenant.Id, "shop-api", profile.Id, principal.Id);

        var activity = await RecordActivityAsync(
            tenant.Id,
            project.Id,
            principal.Id,
            ActivityType.Created,
            "seeded activity",
            DateTimeOffset.UtcNow);

        // There must be no update or delete route for a recorded activity: only GET exists.
        using var put = await _client.PutAsJsonAsync(
            $"/tenants/{tenant.Id}/activities/{activity.Id}",
            new { },
            JsonOptions);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, put.StatusCode);

        using var delete = await _client.DeleteAsync(
            $"/tenants/{tenant.Id}/activities/{activity.Id}");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, delete.StatusCode);

        // The record is unchanged and still obtains.
        Assert.Equal(activity.Id, (await GetActivityAsync(tenant.Id, activity.Id))!.Id);
    }

    [Fact]
    public async Task REV_008_amendment_without_reason_or_reference_conflicts_and_a_valid_one_records()
    {
        var tenant = await CreateTenantAsync("Alpha Corp");
        var principal = await CreatePrincipalAsync(tenant.Id, "a@alphacorp.com", PrincipalType.Human);
        var profile = await CreateLifecycleProfileAsync(tenant.Id, "agile", "Agile", "1.0");
        var project = await CreateProjectAsync(tenant.Id, "shop-api", profile.Id, principal.Id);

        using var conflict = await PostActivityAsync(
            tenant.Id,
            project.Id,
            new RecordActivityRequest(
                ActivityType.Correction,
                principal.Id,
                "correct a flawed diagram",
                ActivityStatus.Recorded,
                DateTimeOffset.UtcNow,
                null,
                null,
                null,
                null,
                Array.Empty<ActivityInputRequest>(),
                Array.Empty<ActivityOutputRequest>()));

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var error = (await conflict.Content.ReadFromJsonAsync<ActivityError>(JsonOptions))!;
        Assert.Equal("amendment-requires-reference", error.Code);
        Assert.NotNull(error.Message);

        // A well-formed amendment (reason + referenced revision) commits successfully.
        using var valid = await PostActivityAsync(
            tenant.Id,
            project.Id,
            new RecordActivityRequest(
                ActivityType.Correction,
                principal.Id,
                "correct a flawed diagram",
                ActivityStatus.Recorded,
                DateTimeOffset.UtcNow,
                null,
                null,
                "diagram was missing the storage layer",
                Guid.NewGuid(),
                Array.Empty<ActivityInputRequest>(),
                Array.Empty<ActivityOutputRequest>()));

        Assert.Equal(HttpStatusCode.Created, valid.StatusCode);
    }

    [Fact]
    public async Task activity_reads_never_disclose_cross_tenant_data()
    {
        var alpha = await CreateTenantAsync("Alpha Corp");
        var beta = await CreateTenantAsync("Beta Corp");

        var principalA = await CreatePrincipalAsync(alpha.Id, "a@alphacorp.com", PrincipalType.Human);
        var principalB = await CreatePrincipalAsync(beta.Id, "b@betacorp.com", PrincipalType.Human);

        var profileA = await CreateLifecycleProfileAsync(alpha.Id, "agile", "Agile", "1.0");
        var profileB = await CreateLifecycleProfileAsync(beta.Id, "agile", "Agile", "1.0");

        var projectA = await CreateProjectAsync(alpha.Id, "shop-api", profileA.Id, principalA.Id);
        var projectB = await CreateProjectAsync(beta.Id, "shop-api", profileB.Id, principalB.Id);

        var itemB = await CreateKnowledgeItemAsync(beta.Id, projectB.Id, "architecture", principalB.Id);
        var revisionB = await AddRevisionAsync(beta.Id, itemB.Id, principalB.Id, 0, "{\"v\":1}");

        var activityB = await RecordActivityAsync(
            beta.Id,
            projectB.Id,
            principalB.Id,
            ActivityType.Generated,
            "generate beta artifact",
            DateTimeOffset.UtcNow,
            outputs: new[] { new ActivityOutputRequest(itemB.Id, revisionB.Id, 0) });

        // HTTP read paths must never disclose beta's activity to tenant A.
        Assert.NotEmpty(await ListActivitiesForProjectAsync(beta.Id, projectB.Id));
        Assert.Empty(await ListActivitiesForProjectAsync(alpha.Id, projectB.Id));
        Assert.Null(await GetActivityAsync(alpha.Id, activityB.Id));
        Assert.Empty(await ListActivitiesForItemAsync(alpha.Id, itemB.Id));
        Assert.Empty(await ListActivitiesByActorAsync(alpha.Id, principalB.Id));

        // Repository read paths: negative disclosure across every read method.
        using var scope = _factory.Services.CreateScope();
        var activities = scope.ServiceProvider.GetRequiredService<IActivityRepository>();

        Assert.Null(await activities.GetByIdAsync(new TenantId(alpha.Id), new ActivityId(activityB.Id)));
        Assert.Empty(await activities.ListForProjectAsync(new TenantId(alpha.Id), new ProjectId(projectB.Id)));
        Assert.Empty(await activities.ListForItemAsync(new TenantId(alpha.Id), new KnowledgeItemId(itemB.Id)));
        Assert.Empty(await activities.ListByActorAsync(new TenantId(alpha.Id), new PrincipalId(principalB.Id)));
    }

    [Fact]
    public async Task REV_007_state_reconstruction_returns_timeline_and_as_of_snapshot()
    {
        var tenant = await CreateTenantAsync("Alpha Corp");
        var principal = await CreatePrincipalAsync(tenant.Id, "a@alphacorp.com", PrincipalType.Human);
        var profile = await CreateLifecycleProfileAsync(tenant.Id, "agile", "Agile", "1.0");
        var project = await CreateProjectAsync(tenant.Id, "shop-api", profile.Id, principal.Id);

        var item = await CreateKnowledgeItemAsync(tenant.Id, project.Id, "architecture", principal.Id);
        var first = await AddRevisionAsync(tenant.Id, item.Id, principal.Id, 0, "{\"v\":1}");
        Assert.Equal(1, first.RevisionNumber);

        await Task.Delay(5);
        var asOf = DateTimeOffset.UtcNow;

        var second = await AddRevisionAsync(tenant.Id, item.Id, principal.Id, 1, "{\"v\":2}");
        Assert.Equal(2, second.RevisionNumber);

        var activity = await RecordActivityAsync(
            tenant.Id,
            project.Id,
            principal.Id,
            ActivityType.Generated,
            "generate revision 2",
            DateTimeOffset.UtcNow,
            outputs: new[] { new ActivityOutputRequest(item.Id, second.Id, 0) });

        // Latest reconstruction: revision 2, and the timeline carries the recorded activity.
        var latest = await GetStateAsync(tenant.Id, project.Id);
        Assert.Equal(2, Assert.Single(latest.Items).RevisionNumber);
        Assert.Equal(second.Id, Assert.Single(latest.Items).RevisionId);
        Assert.Single(latest.Activities);
        Assert.Equal(activity.Id, latest.Activities[0].Id);

        // As-of reconstruction rolls the item back to the revision recorded before the timestamp.
        var rolledBack = await GetStateAsync(tenant.Id, project.Id, asOf);
        var snapshot = Assert.Single(rolledBack.Items);
        Assert.Equal(1, snapshot.RevisionNumber);
        Assert.Equal(first.Id, snapshot.RevisionId);
    }

    [Fact]
    public async Task PRO_008_provenance_export_projects_the_prov_o_shape()
    {
        var tenant = await CreateTenantAsync("Alpha Corp");
        var principal = await CreatePrincipalAsync(tenant.Id, "a@alphacorp.com", PrincipalType.Human);
        var profile = await CreateLifecycleProfileAsync(tenant.Id, "agile", "Agile", "1.0");
        var project = await CreateProjectAsync(tenant.Id, "shop-api", profile.Id, principal.Id);

        var item = await CreateKnowledgeItemAsync(tenant.Id, project.Id, "architecture", principal.Id);
        var revision = await AddRevisionAsync(tenant.Id, item.Id, principal.Id, 0, "{\"v\":1}");

        var activity = await RecordActivityAsync(
            tenant.Id,
            project.Id,
            principal.Id,
            ActivityType.Generated,
            "generate first revision",
            DateTimeOffset.UtcNow,
            outputs: new[] { new ActivityOutputRequest(item.Id, revision.Id, 0) });

        var records = await GetProvenanceAsync(tenant.Id, project.Id);
        var record = Assert.Single(records);

        Assert.Equal(activity.Id, record.ActivityId);
        Assert.Equal(ActivityType.Generated.ToString(), record.ActivityType);
        Assert.Equal(principal.Id, record.Actor);

        var generated = Assert.Single(record.Generated);
        Assert.Equal(item.Id, generated.ItemId);
        Assert.Equal(revision.Id, generated.RevisionId);
        Assert.Empty(record.Used);
    }

    [Fact]
    public async Task list_by_actor_and_by_item_return_the_linked_activity()
    {
        var tenant = await CreateTenantAsync("Alpha Corp");
        var principal = await CreatePrincipalAsync(tenant.Id, "a@alphacorp.com", PrincipalType.Human);
        var profile = await CreateLifecycleProfileAsync(tenant.Id, "agile", "Agile", "1.0");
        var project = await CreateProjectAsync(tenant.Id, "shop-api", profile.Id, principal.Id);

        var item = await CreateKnowledgeItemAsync(tenant.Id, project.Id, "architecture", principal.Id);
        var revision = await AddRevisionAsync(tenant.Id, item.Id, principal.Id, 0, "{\"v\":1}");

        var activity = await RecordActivityAsync(
            tenant.Id,
            project.Id,
            principal.Id,
            ActivityType.Generated,
            "generate linked artifact",
            DateTimeOffset.UtcNow,
            outputs: new[] { new ActivityOutputRequest(item.Id, revision.Id, 0) });

        var byActor = await ListActivitiesByActorAsync(tenant.Id, principal.Id);
        Assert.Contains(byActor, a => a.Id == activity.Id);

        var byItem = await ListActivitiesForItemAsync(tenant.Id, item.Id);
        Assert.Contains(byItem, a => a.Id == activity.Id);
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

    private async Task<KnowledgeRevisionResponse> AddRevisionAsync(Guid tenantId, Guid itemId, Guid actor, int expected, string content)
    {
        await SeedArchitectureSchemaAsync();

        using var client = _factory.CreateAuthenticatedHumanClient(tenantId, actor);
        using var response = await client.PostAsJsonAsync(
            $"/tenants/{tenantId}/knowledge/{itemId}/revisions",
            new AddRevisionRequest(expected, "1.0", content, actor, "Baseline", null, null),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<KnowledgeRevisionResponse>(JsonOptions))!;
    }

    private async Task SeedArchitectureSchemaAsync()
    {
        var existing = await FindSchemaAsync("architecture.mmd", "1.0");
        if (existing is not null)
        {
            if (existing.Status != SchemaStatus.Published)
            {
                await PublishSchemaAsync(existing.Id);
            }

            return;
        }

        var created = await RegisterSchemaAsync("architecture.mmd", "1.0", "{\"type\":\"object\"}");
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

    private async Task<HttpResponseMessage> PostActivityAsync(Guid tenantId, Guid projectId, RecordActivityRequest request)
    {
        using var client = _factory.CreateAuthenticatedHumanClient(tenantId, request.Actor);
        return await client.PostAsJsonAsync(
            $"/tenants/{tenantId}/projects/{projectId}/activities",
            request,
            JsonOptions);
    }

    private async Task<ActivityResponse> RecordActivityAsync(
        Guid tenantId,
        Guid projectId,
        Guid actor,
        ActivityType type,
        string purpose,
        DateTimeOffset startedAt,
        DateTimeOffset? endedAt = null,
        string? correlationId = null,
        string? reason = null,
        Guid? reference = null,
        IReadOnlyList<ActivityInputRequest>? inputs = null,
        IReadOnlyList<ActivityOutputRequest>? outputs = null)
    {
        using var response = await PostActivityAsync(
            tenantId,
            projectId,
            new RecordActivityRequest(
                type,
                actor,
                purpose,
                ActivityStatus.Recorded,
                startedAt,
                endedAt,
                correlationId,
                reason,
                reference,
                inputs ?? Array.Empty<ActivityInputRequest>(),
                outputs ?? Array.Empty<ActivityOutputRequest>()));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ActivityResponse>(JsonOptions))!;
    }

    private async Task<List<ActivityResponse>> ListActivitiesForProjectAsync(Guid tenantId, Guid projectId)
    {
        using var response = await _client.GetAsync($"/tenants/{tenantId}/projects/{projectId}/activities");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<ActivityResponse>>(JsonOptions))!;
    }

    private async Task<ActivityResponse?> GetActivityAsync(Guid tenantId, Guid activityId)
    {
        using var response = await _client.GetAsync($"/tenants/{tenantId}/activities/{activityId}");
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ActivityResponse>(JsonOptions);
    }

    private async Task<List<ActivityResponse>> ListActivitiesForItemAsync(Guid tenantId, Guid itemId)
    {
        using var response = await _client.GetAsync($"/tenants/{tenantId}/knowledge/{itemId}/activities");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<ActivityResponse>>(JsonOptions))!;
    }

    private async Task<List<ActivityResponse>> ListActivitiesByActorAsync(Guid tenantId, Guid actorId)
    {
        using var response = await _client.GetAsync($"/tenants/{tenantId}/principals/{actorId}/activities");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<ActivityResponse>>(JsonOptions))!;
    }

    private async Task<ProjectStateReconstructionResponse> GetStateAsync(Guid tenantId, Guid projectId, DateTimeOffset? at = null)
    {
        var path = $"/tenants/{tenantId}/projects/{projectId}/state";
        if (at is not null)
        {
            path += $"?at={Uri.EscapeDataString(at.Value.ToString("O"))}";
        }

        using var response = await _client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectStateReconstructionResponse>(JsonOptions))!;
    }

    private async Task<List<ActivityProvenanceResponse>> GetProvenanceAsync(Guid tenantId, Guid projectId)
    {
        using var response = await _client.GetAsync($"/tenants/{tenantId}/projects/{projectId}/provenance");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<ActivityProvenanceResponse>>(JsonOptions))!;
    }
}
