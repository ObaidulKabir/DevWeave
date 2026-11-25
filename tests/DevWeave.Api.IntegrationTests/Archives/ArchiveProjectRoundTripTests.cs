using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevWeave.Api.Endpoints.Archives;
using DevWeave.Application.Abstractions;
using DevWeave.Application.Archives;
using DevWeave.Contracts.Activities.V1;
using DevWeave.Contracts.Evidence.V1;
using DevWeave.Contracts.Identity.V1;
using DevWeave.Contracts.Knowledge.V1;
using DevWeave.Contracts.Relations.V1;
using DevWeave.Contracts.Lifecycle.V1;
using DevWeave.Contracts.Planned.V1;
using DevWeave.Contracts.Projects.V1;
using DevWeave.Domain.Activities;
using DevWeave.Domain.Archives;
using DevWeave.Domain.Evidence;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DevWeave.Api.IntegrationTests.Archives;

public sealed class ArchiveProjectRoundTripTests : IClassFixture<IdentityTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };

    private readonly IdentityTestFactory _factory;
    private readonly HttpClient _client;

    public ArchiveProjectRoundTripTests(IdentityTestFactory factory)
    {
        _factory = factory;
        _factory.PrepareDatabase();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Export_stores_the_bundle_and_import_replays_it_into_an_empty_project()
    {
        var tenant = await CreateTenantAsync("Archive Corp");
        var owner = await CreateOwnerAsync(tenant.Id, "owner@archive.test");
        var profile = await CreateLifecycleProfileAsync(tenant.Id, "archive", "Archive", "1.0");
        var source = await CreateProjectAsync(tenant.Id, "archive-source", profile.Id, owner.Id);
        var destination = await CreateProjectAsync(tenant.Id, "archive-destination", profile.Id, owner.Id);
        var client = _factory.CreateAuthenticatedHumanClient(tenant.Id, owner.Id);

        var created = await PostAsync<KnowledgeItemResponse>(
            client,
            $"/tenants/{tenant.Id}/projects/{source.Id}/knowledge",
            new CreateKnowledgeItemRequest(
                "FR-1", "Checkout", Classification.Internal, RepresentationKind.StructuredData,
                "requirements.functional", "1.0", owner.Id),
            HttpStatusCode.Created);
        var decision = await PostAsync<KnowledgeItemResponse>(
            client,
            $"/tenants/{tenant.Id}/projects/{source.Id}/knowledge",
            new CreateKnowledgeItemRequest(
                "ADR-1", "Checkout decision", Classification.Internal, RepresentationKind.StructuredData,
                "architecture.decision", "1.0", owner.Id),
            HttpStatusCode.Created);
        const string requirement = """{"description":"Checkout","acceptanceCriteria":[{"id":"AC-1","statement":"The buyer can pay"}]}""";
        var revision = await PostAsync<KnowledgeRevisionResponse>(
            client,
            $"/tenants/{tenant.Id}/knowledge/{created.Id}/revisions",
            new AddRevisionRequest(0, "1.0", requirement, owner.Id, "Initial", null, null),
            HttpStatusCode.Created);
        await PostAsync<KnowledgeRevisionResponse>(
            client,
            $"/tenants/{tenant.Id}/knowledge/{decision.Id}/revisions",
            new AddRevisionRequest(0, "1.0", """{"context":"Checkout","decision":"Use the ledger"}""", owner.Id, "Initial", null, null),
            HttpStatusCode.Created);
        await PostAsync<KnowledgeItemResponse>(
            client,
            $"/tenants/{tenant.Id}/knowledge/{created.Id}/approve",
            new ReviewRevisionRequest(revision.Id, "Approved for archive"));
        var claim = await PostAsync<ActivityResponse>(
            client,
            $"/tenants/{tenant.Id}/projects/{source.Id}/activities",
            new RecordActivityRequest(
                ActivityType.Claim, owner.Id, "Record the checkout claim", ActivityStatus.Completed,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null, null,
                Array.Empty<ActivityInputRequest>(),
                [new ActivityOutputRequest(created.Id, revision.Id, 0)]),
            HttpStatusCode.Created);
        var evidence = await PostAsync<EvidenceResponse>(
            client,
            $"/tenants/{tenant.Id}/claims/{claim.Id}/evidence",
            new RecordEvidenceRequest("test-run", "sha256:abc", null, created.Id, revision.Id, "Checkout"),
            HttpStatusCode.Created);
        await PostAsync<EvidenceResponse>(
            client,
            $"/tenants/{tenant.Id}/evidence/{evidence.Id}/review",
            new ReviewEvidenceRequest(EvidenceReviewState.Accepted, owner.Id));
        await PostAsync<RelationshipResponse>(
            client,
            $"/tenants/{tenant.Id}/projects/{source.Id}/relationships",
            new CreateRelationshipRequest(decision.Id, created.Id, "satisfies", owner.Id, "The decision covers checkout"),
            HttpStatusCode.Created);
        var verified = await PostAsync<KnowledgeItemResponse>(
            client,
            $"/tenants/{tenant.Id}/knowledge/{created.Id}/verify",
            new { });
        Assert.Equal(InfoState.Verified, verified.State);

        var exported = await PostAsync<ArchiveJobView>(
            client,
            $"/tenants/{tenant.Id}/projects/{source.Id}/archives/export",
            new ExportDraft("export-checkout"),
            HttpStatusCode.Accepted);
        var again = await PostAsync<ArchiveJobView>(
            client,
            $"/tenants/{tenant.Id}/projects/{source.Id}/archives/export",
            new ExportDraft("export-checkout"),
            HttpStatusCode.Accepted);

        Assert.Equal(exported.Id, again.Id);
        Assert.Equal("devweave.archive.v1", exported.FormatVersion);
        Assert.Equal("Export", exported.Kind);
        Assert.False(string.IsNullOrWhiteSpace(exported.Checksum));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
            var row = await db.ArchiveJobs.SingleAsync(job => job.Id == exported.Id);
            Assert.Equal(exported.ObjectKey, row.ObjectKey);
            Assert.Equal(exported.Checksum, row.Checksum);
            Assert.Equal("Export", row.Kind);

            var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
            await using var stream = await storage.GetAsync(exported.ObjectKey);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            var document = new ArchiveReaderRegistry().Read(buffer.ToArray());
            Assert.Equal(exported.Checksum, document.Checksum);
            Assert.Equal("Verified", Assert.Single(document.Payload.Knowledge, row => row.Key == "FR-1").State);
            Assert.Contains(document.Payload.Knowledge, row => row.Key == "ADR-1");
            Assert.Contains(document.Payload.Revisions, row => row.ItemId == created.Id);
            Assert.Equal("satisfies", Assert.Single(document.Payload.Relationships).RelationTypeCode);
            Assert.Equal("Accepted", Assert.Single(document.Payload.Evidence).ReviewState);
        }

        var occupied = await client.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/projects/{source.Id}/archives/import",
            new ProjectArchiveImportRequest(exported.ObjectKey, "import-into-source"));
        Assert.Equal(HttpStatusCode.Conflict, occupied.StatusCode);
        Assert.Contains("no knowledge", await occupied.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var imported = await PostAsync<ArchiveJobView>(
            client,
            $"/tenants/{tenant.Id}/projects/{destination.Id}/archives/import",
            new ProjectArchiveImportRequest(exported.ObjectKey, "import-checkout"),
            HttpStatusCode.Accepted);
        var importedAgain = await PostAsync<ArchiveJobView>(
            client,
            $"/tenants/{tenant.Id}/projects/{destination.Id}/archives/import",
            new ProjectArchiveImportRequest(exported.ObjectKey, "import-checkout"),
            HttpStatusCode.Accepted);

        Assert.Equal(imported.Id, importedAgain.Id);
        Assert.Equal("Import", imported.Kind);
        Assert.NotEqual(exported.Id, imported.Id);

        var replayed = await GetAsync<List<KnowledgeItemResponse>>(
            client, $"/tenants/{tenant.Id}/projects/{destination.Id}/knowledge");
        var copy = Assert.Single(replayed, item => item.Key == "FR-1");
        Assert.Equal("Checkout", copy.Title);
        Assert.Equal(InfoState.Verified, copy.State);
        Assert.NotEqual(created.Id, copy.Id);
        Assert.NotNull(copy.CurrentRevisionId);

        var revisions = await GetAsync<List<KnowledgeRevisionResponse>>(
            client, $"/tenants/{tenant.Id}/knowledge/{copy.Id}/revisions");
        var copiedRevision = Assert.Single(revisions);
        Assert.Equal(copy.CurrentRevisionId, copiedRevision.Id);
        Assert.Contains("AC-1", copiedRevision.Content, StringComparison.Ordinal);
        Assert.NotEqual(revision.Id, copiedRevision.Id);

        var edges = await GetAsync<List<RelationshipResponse>>(
            client, $"/tenants/{tenant.Id}/projects/{destination.Id}/relationships");
        var copiedEdge = Assert.Single(edges);
        Assert.Equal("satisfies", copiedEdge.RelationTypeCode);
        Assert.Equal(copy.Id, copiedEdge.TargetItemId);

        var copiedEvidence = Assert.Single(await GetAsync<List<EvidenceResponse>>(
            client, $"/tenants/{tenant.Id}/projects/{destination.Id}/evidence"));
        Assert.Equal(EvidenceReviewState.Accepted, copiedEvidence.ReviewState);
        Assert.Equal(copy.Id, copiedEvidence.CriterionItemId);
        Assert.Equal(copiedRevision.Id, copiedEvidence.CriterionRevisionId);

        using var after = _factory.Services.CreateScope();
        var jobs = after.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
        var importRow = await jobs.ArchiveJobs.SingleAsync(job => job.Id == imported.Id);
        Assert.Contains(created.Id.ToString(), importRow.IdentityMapJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(copy.Id.ToString(), importRow.IdentityMapJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(revision.Id.ToString(), importRow.IdentityMapJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(copiedRevision.Id.ToString(), importRow.IdentityMapJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Export_rejects_a_package_the_content_policy_refuses()
    {
        var tenant = await CreateTenantAsync("Archive Policy Corp");
        var owner = await CreateOwnerAsync(tenant.Id, "owner@archive-policy.test");
        var profile = await CreateLifecycleProfileAsync(tenant.Id, "archive-policy", "Archive policy", "1.0");
        var project = await CreateProjectAsync(tenant.Id, "archive-policy", profile.Id, owner.Id);
        var client = _factory.CreateAuthenticatedHumanClient(tenant.Id, owner.Id);

        await PostAsync<KnowledgeItemResponse>(
            client,
            $"/tenants/{tenant.Id}/projects/{project.Id}/knowledge",
            new CreateKnowledgeItemRequest(
                "SECRET-1", "AKIAEXAMPLEKEY", Classification.Internal, RepresentationKind.Text,
                "requirements.functional", "1.0", owner.Id),
            HttpStatusCode.Created);

        using var response = await client.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/projects/{project.Id}/archives/export",
            new ExportDraft("export-secret"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("content policy", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    private async Task<T> PostAsync<T>(HttpClient client, string url, object body, HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var response = await client.PostAsJsonAsync(url, body, JsonOptions);
        Assert.Equal(expected, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private async Task<T> GetAsync<T>(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
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

        using var activate = await _client.PostAsync($"/tenants/{tenantId}/members/{membership.Id}/activate", content: null);
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
            new CreateProjectRequest("Archive project", key, null, lifecycleProfileId, createdBy),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions))!;
    }
}
