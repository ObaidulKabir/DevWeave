using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevWeave.Application.Abstractions;
using DevWeave.Contracts.Activities.V1;
using DevWeave.Contracts.Artifacts.V1;
using DevWeave.Contracts.Evidence.V1;
using DevWeave.Contracts.Identity.V1;
using DevWeave.Contracts.Lifecycle.V1;
using DevWeave.Contracts.Projects.V1;
using DevWeave.Domain.Activities;
using DevWeave.Domain.Artifacts;
using DevWeave.Domain.Evidence;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace DevWeave.Api.IntegrationTests;

/// <summary>
/// Round-trips the Artifact and Evidence HTTP surfaces (SRS ART-001..ART-004, ART-006, PRO-006) through
/// the full API + application + persistence pipeline against a real database and an in-memory object
/// store, verifying upload/read/list/version/verify/download for artifacts and record/review/list/
/// verify for evidence.
/// </summary>
public sealed class ArtifactEvidenceRoundTripIntegrationTests : IClassFixture<IdentityTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };

    private readonly IdentityTestFactory _factory;
    private readonly HttpClient _client;

    public ArtifactEvidenceRoundTripIntegrationTests(IdentityTestFactory factory)
    {
        _factory = factory;
        _factory.PrepareDatabase();
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task ART_001_ART_003_ART_004_ART_006_artifact_round_trips_through_the_api()
    {
        var tenant = await CreateTenantAsync("Alpha Corp");
        var actor = await CreatePrincipalAsync(tenant.Id, "a@alphacorp.com", PrincipalType.Human);
        var profile = await CreateLifecycleProfileAsync(tenant.Id, "agile", "Agile", "1.0");
        var project = await CreateProjectAsync(tenant.Id, "shop-api", profile.Id, actor.Id);

        var first = await UploadArtifactAsync(tenant.Id, project.Id, "notes.txt", "notes.txt", "text/plain", "Internal", actor.Id, "first version");
        Assert.Equal(1, first.Version);
        Assert.Equal("text/plain", first.MediaType);
        Assert.False(string.IsNullOrWhiteSpace(first.Checksum));
        Assert.Equal("sha256", first.ChecksumAlgorithm);

        // ART-003: a replacement upload creates a new immutable version rather than erasing the prior one.
        var second = await UploadArtifactAsync(tenant.Id, project.Id, "notes.txt", "notes.txt", "text/plain", "Internal", actor.Id, "second version");
        Assert.Equal(2, second.Version);
        Assert.NotEqual(first.Id, second.Id);

        var listed = await ListArtifactsAsync(tenant.Id, project.Id);
        Assert.Equal(2, listed.Count);

        var versions = await ListArtifactVersionsAsync(tenant.Id, project.Id, "notes.txt");
        Assert.Equal(2, versions.Count);
        Assert.Contains(versions, a => a.Id == first.Id && a.Version == 1);
        Assert.Contains(versions, a => a.Id == second.Id && a.Version == 2);

        var fetched = await GetArtifactAsync(tenant.Id, second.Id);
        Assert.NotNull(fetched);
        Assert.Equal(second.Id, fetched.Id);

        // ART-006: the ingest checksum revalidates against the stored bytes.
        var verification = await VerifyArtifactAsync(tenant.Id, second.Id);
        Assert.True(verification.Match);
        Assert.Equal(second.Checksum, verification.ExpectedChecksum);
        Assert.Equal(second.Checksum, verification.ActualChecksum);

        // ART-004: downloads are short-lived presigned grants, never a permanent public URL.
        var download = await GetArtifactDownloadUrlAsync(tenant.Id, second.Id);
        Assert.False(string.IsNullOrWhiteSpace(download.DownloadUrl));
        Assert.Contains("X-Amz-", download.DownloadUrl);
        Assert.True(download.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task ART_008_source_code_artifact_is_quarantined_and_not_served()
    {
        var tenant = await CreateTenantAsync("Alpha Corp");
        var actor = await CreatePrincipalAsync(tenant.Id, "a@alphacorp.com", PrincipalType.Human);
        var profile = await CreateLifecycleProfileAsync(tenant.Id, "agile", "Agile", "1.0");
        var project = await CreateProjectAsync(tenant.Id, "shop-api", profile.Id, actor.Id);

        // Source-code content is quarantined by the shared content policy, not stored canonically.
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("class Program {}"));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", "Program.cs");
        form.Add(new StringContent("Program.cs"), "key");
        form.Add(new StringContent("Program.cs"), "fileName");
        form.Add(new StringContent("text/plain"), "mediaType");
        form.Add(new StringContent("Internal"), "classification");
        form.Add(new StringContent(actor.Id.ToString()), "creator");

        using var client = _factory.CreateAuthenticatedHumanClient(tenant.Id, actor.Id);
        using var response = await client.PostAsync($"/tenants/{tenant.Id}/projects/{project.Id}/artifacts", form);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<ArtifactError>(JsonOptions))!;
        Assert.Equal("quarantined-content", error.Code);

        // The version record is retained for review but flagged, and downloads are refused (ART-004/008).
        var listed = await ListArtifactsAsync(tenant.Id, project.Id);
        var quarantined = Assert.Single(listed);
        Assert.Equal(ArtifactStatus.Quarantined, quarantined.Status);

        using var download = await _client.GetAsync($"/tenants/{tenant.Id}/artifacts/{quarantined.Id}/download");
        Assert.Equal(HttpStatusCode.NotFound, download.StatusCode);
    }

    [Fact]
    public async Task ART_003_logical_keys_with_same_normalized_path_keep_distinct_immutable_objects()
    {
        var tenant = await CreateTenantAsync("Alpha Corp");
        var actor = await CreatePrincipalAsync(tenant.Id, "a@alphacorp.com", PrincipalType.Human);
        var profile = await CreateLifecycleProfileAsync(tenant.Id, "agile", "Agile", "1.0");
        var project = await CreateProjectAsync(tenant.Id, "shop-api", profile.Id, actor.Id);

        var upper = await UploadArtifactAsync(tenant.Id, project.Id, "Design", "design.txt",
            "text/plain", "Internal", actor.Id, "upper-case payload");
        var lower = await UploadArtifactAsync(tenant.Id, project.Id, "design", "design.txt",
            "text/plain", "Internal", actor.Id, "lower-case payload");

        Assert.Equal(1, upper.Version);
        Assert.Equal(1, lower.Version);
        Assert.NotEqual(upper.ObjectKey, lower.ObjectKey);
        Assert.True((await VerifyArtifactAsync(tenant.Id, upper.Id)).Match);
        Assert.True((await VerifyArtifactAsync(tenant.Id, lower.Id)).Match);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DevWeave.Infrastructure.Persistence.DevWeaveDbContext>();
        db.Artifacts.Add(Artifact.Create(
            new TenantId(tenant.Id),
            new ProjectId(project.Id),
            "Design",
            upper.Version,
            "text/plain",
            1,
            "duplicate",
            "sha256",
            $"{tenant.Id:N}/{project.Id:N}/artifacts/{Guid.NewGuid():N}",
            Classification.Internal,
            new PrincipalId(actor.Id),
            ArtifactStatus.Stored));

        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await Assert.ThrowsAsync<ConflictException>(() => unitOfWork.SaveChangesAsync());
    }

    [Fact]
    public async Task PRO_006_evidence_round_trips_record_review_list_and_claim_verification()
    {
        var tenant = await CreateTenantAsync("Alpha Corp");
        var actor = await CreatePrincipalAsync(tenant.Id, "a@alphacorp.com", PrincipalType.Human);
        var profile = await CreateLifecycleProfileAsync(tenant.Id, "agile", "Agile", "1.0");
        var project = await CreateProjectAsync(tenant.Id, "shop-api", profile.Id, actor.Id);

        var claim = await CreateClaimAsync(tenant.Id, project.Id, actor.Id);

        var recorded = await RecordEvidenceAsync(tenant.Id, claim.Id, "screenshot", "sha256:aa", null, "AC-1");
        Assert.Equal(EvidenceReviewState.Unreviewed, recorded.ReviewState);
        Assert.Null(recorded.ReviewedBy);

        var forClaim = await ListEvidenceForClaimAsync(tenant.Id, claim.Id);
        Assert.Single(forClaim);
        Assert.Equal(recorded.Id, forClaim[0].Id);

        var forProject = await ListEvidenceForProjectAsync(tenant.Id, project.Id);
        Assert.Single(forProject);
        Assert.Equal(recorded.Id, forProject[0].Id);

        // A claim stays unverified until its evidence is accepted (PRO-005/PRO-006).
        var before = await GetClaimVerificationAsync(tenant.Id, claim.Id);
        Assert.False(before.IsVerified);

        var reviewed = await ReviewEvidenceAsync(tenant.Id, recorded.Id, EvidenceReviewState.Accepted, actor.Id);
        Assert.Equal(EvidenceReviewState.Accepted, reviewed.ReviewState);
        Assert.Equal(actor.Id, reviewed.ReviewedBy);

        var after = await GetClaimVerificationAsync(tenant.Id, claim.Id);
        Assert.True(after.IsVerified);
        Assert.Single(after.Evidence);
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

    private async Task<ActivityResponse> CreateClaimAsync(Guid tenantId, Guid projectId, Guid actorId)
    {
        using var client = _factory.CreateAuthenticatedHumanClient(tenantId, actorId);
        using var response = await client.PostAsJsonAsync(
            $"/tenants/{tenantId}/projects/{projectId}/activities",
            new RecordActivityRequest(
                ActivityType.Claim,
                actorId,
                "blueprint meets AC-1",
                ActivityStatus.Completed,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                null,
                null,
                null,
                Array.Empty<ActivityInputRequest>(),
                Array.Empty<ActivityOutputRequest>()),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ActivityResponse>(JsonOptions))!;
    }

    private async Task<ArtifactResponse> UploadArtifactAsync(
        Guid tenantId,
        Guid projectId,
        string key,
        string fileName,
        string mediaType,
        string classification,
        Guid creator,
        string content)
    {
        using var form = new MultipartFormDataContent();

        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        form.Add(file, "file", fileName);

        form.Add(new StringContent(key), "key");
        form.Add(new StringContent(fileName), "fileName");
        form.Add(new StringContent(mediaType), "mediaType");
        form.Add(new StringContent(classification), "classification");
        form.Add(new StringContent(creator.ToString()), "creator");

        using var client = _factory.CreateAuthenticatedHumanClient(tenantId, creator);
        using var response = await client.PostAsync($"/tenants/{tenantId}/projects/{projectId}/artifacts", form);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ArtifactResponse>(JsonOptions))!;
    }

    private async Task<List<ArtifactResponse>> ListArtifactsAsync(Guid tenantId, Guid projectId)
    {
        using var response = await _client.GetAsync($"/tenants/{tenantId}/projects/{projectId}/artifacts");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<ArtifactResponse>>(JsonOptions))!;
    }

    private async Task<List<ArtifactResponse>> ListArtifactVersionsAsync(Guid tenantId, Guid projectId, string key)
    {
        using var response = await _client.GetAsync($"/tenants/{tenantId}/projects/{projectId}/artifacts/versions?key={Uri.EscapeDataString(key)}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<ArtifactResponse>>(JsonOptions))!;
    }

    private async Task<ArtifactResponse?> GetArtifactAsync(Guid tenantId, Guid artifactId)
    {
        using var response = await _client.GetAsync($"/tenants/{tenantId}/artifacts/{artifactId}");
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ArtifactResponse>(JsonOptions);
    }

    private async Task<ArtifactVerificationResponse> VerifyArtifactAsync(Guid tenantId, Guid artifactId)
    {
        using var response = await _client.GetAsync($"/tenants/{tenantId}/artifacts/{artifactId}/verify");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ArtifactVerificationResponse>(JsonOptions))!;
    }

    private async Task<ArtifactDownloadResponse> GetArtifactDownloadUrlAsync(Guid tenantId, Guid artifactId)
    {
        using var response = await _client.GetAsync($"/tenants/{tenantId}/artifacts/{artifactId}/download");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ArtifactDownloadResponse>(JsonOptions))!;
    }

    private async Task<EvidenceResponse> RecordEvidenceAsync(
        Guid tenantId,
        Guid claimId,
        string type,
        string? integrityHash,
        Guid? relatedArtifactId,
        string relatedCriterion)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/claims/{claimId}/evidence",
            new RecordEvidenceRequest(type, integrityHash, relatedArtifactId, relatedCriterion),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EvidenceResponse>(JsonOptions))!;
    }

    private async Task<EvidenceResponse> ReviewEvidenceAsync(Guid tenantId, Guid evidenceId, EvidenceReviewState state, Guid reviewer)
    {
        using var client = _factory.CreateAuthenticatedHumanClient(tenantId, reviewer);
        using var response = await client.PostAsJsonAsync(
            $"/tenants/{tenantId}/evidence/{evidenceId}/review",
            new ReviewEvidenceRequest(state, reviewer),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EvidenceResponse>(JsonOptions))!;
    }

    private async Task<List<EvidenceResponse>> ListEvidenceForClaimAsync(Guid tenantId, Guid claimId)
    {
        using var response = await _client.GetAsync($"/tenants/{tenantId}/claims/{claimId}/evidence");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<EvidenceResponse>>(JsonOptions))!;
    }

    private async Task<List<EvidenceResponse>> ListEvidenceForProjectAsync(Guid tenantId, Guid projectId)
    {
        using var response = await _client.GetAsync($"/tenants/{tenantId}/projects/{projectId}/evidence");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<EvidenceResponse>>(JsonOptions))!;
    }

    private async Task<ClaimVerificationResponse> GetClaimVerificationAsync(Guid tenantId, Guid claimId)
    {
        using var response = await _client.GetAsync($"/tenants/{tenantId}/claims/{claimId}/verification");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ClaimVerificationResponse>(JsonOptions))!;
    }
}

/// <summary>
/// Proves the deny-by-default tenant isolation invariants for the Artifact and Evidence surfaces
/// (SRS ART-002, PRO-006): a query scoped to tenant A must never disclose tenant B artifacts or
/// evidence, across every HTTP and repository read path.
/// </summary>
public sealed class ArtifactEvidenceTenantIsolationIntegrationTests : IClassFixture<IdentityTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };

    private readonly IdentityTestFactory _factory;
    private readonly HttpClient _client;

    public ArtifactEvidenceTenantIsolationIntegrationTests(IdentityTestFactory factory)
    {
        _factory = factory;
        _factory.PrepareDatabase();
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task ART_002_PRO_006_cross_tenant_artifact_and_evidence_reads_never_disclose_data()
    {
        var alpha = await CreateTenantAsync("Alpha Corp");
        var beta = await CreateTenantAsync("Beta Corp");

        var actorA = await CreatePrincipalAsync(alpha.Id, "a@alphacorp.com", PrincipalType.Human);
        var actorB = await CreatePrincipalAsync(beta.Id, "b@betacorp.com", PrincipalType.Human);

        var profileA = await CreateLifecycleProfileAsync(alpha.Id, "agile", "Agile", "1.0");
        var profileB = await CreateLifecycleProfileAsync(beta.Id, "agile", "Agile", "1.0");

        var projectA = await CreateProjectAsync(alpha.Id, "shop-api", profileA.Id, actorA.Id);
        var projectB = await CreateProjectAsync(beta.Id, "shop-api", profileB.Id, actorB.Id);

        var artifactB = await UploadArtifactAsync(beta.Id, projectB.Id, "notes.txt", "notes.txt", "text/plain", "Internal", actorB.Id, "beta secret");
        var claimB = await CreateClaimAsync(beta.Id, projectB.Id, actorB.Id);
        var evidenceB = await RecordEvidenceAsync(beta.Id, claimB.Id, "screenshot", "sha256:aa", null, "AC-1");

        // HTTP artifact read paths must not disclose beta's artifact to alpha.
        Assert.Null(await GetArtifactAsync(alpha.Id, artifactB.Id));
        Assert.Empty(await ListArtifactsAsync(alpha.Id, projectB.Id));
        Assert.Empty(await ListArtifactVersionsAsync(alpha.Id, projectB.Id, "notes.txt"));
        Assert.Equal(HttpStatusCode.NotFound, (await RawDownloadAsync(alpha.Id, artifactB.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await RawVerifyAsync(alpha.Id, artifactB.Id)).StatusCode);

        // HTTP evidence read paths must not disclose beta's evidence to alpha.
        Assert.Empty(await ListEvidenceForClaimAsync(alpha.Id, claimB.Id));
        Assert.Empty(await ListEvidenceForProjectAsync(alpha.Id, projectB.Id));
        Assert.Equal(HttpStatusCode.NotFound, (await RawClaimVerificationAsync(alpha.Id, claimB.Id)).StatusCode);

        // Repository read paths (negative disclosure across every read method).
        using var scope = _factory.Services.CreateScope();
        var artifacts = scope.ServiceProvider.GetRequiredService<IArtifactRepository>();
        var evidence = scope.ServiceProvider.GetRequiredService<IEvidenceRepository>();

        Assert.Null(await artifacts.GetByIdAsync(new TenantId(alpha.Id), new ArtifactId(artifactB.Id)));
        Assert.Empty(await artifacts.ListForProjectAsync(new TenantId(alpha.Id), new ProjectId(projectB.Id)));
        Assert.Empty(await artifacts.ListVersionsAsync(new TenantId(alpha.Id), new ProjectId(projectB.Id), "notes.txt"));

        Assert.Null(await evidence.GetByIdAsync(new TenantId(alpha.Id), new EvidenceId(evidenceB.Id)));
        Assert.Empty(await evidence.ListForClaimAsync(new TenantId(alpha.Id), new ActivityId(claimB.Id)));
        Assert.Empty(await evidence.ListForProjectAsync(new TenantId(alpha.Id), new ProjectId(projectB.Id)));
    }

    private async Task<HttpResponseMessage> RawDownloadAsync(Guid tenantId, Guid artifactId) =>
        await _client.GetAsync($"/tenants/{tenantId}/artifacts/{artifactId}/download");

    private async Task<HttpResponseMessage> RawVerifyAsync(Guid tenantId, Guid artifactId) =>
        await _client.GetAsync($"/tenants/{tenantId}/artifacts/{artifactId}/verify");

    private async Task<HttpResponseMessage> RawClaimVerificationAsync(Guid tenantId, Guid claimId) =>
        await _client.GetAsync($"/tenants/{tenantId}/claims/{claimId}/verification");

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

    private async Task<ActivityResponse> CreateClaimAsync(Guid tenantId, Guid projectId, Guid actorId)
    {
        using var client = _factory.CreateAuthenticatedHumanClient(tenantId, actorId);
        using var response = await client.PostAsJsonAsync(
            $"/tenants/{tenantId}/projects/{projectId}/activities",
            new RecordActivityRequest(
                ActivityType.Claim,
                actorId,
                "blueprint meets AC-1",
                ActivityStatus.Completed,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                null,
                null,
                null,
                Array.Empty<ActivityInputRequest>(),
                Array.Empty<ActivityOutputRequest>()),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ActivityResponse>(JsonOptions))!;
    }

    private async Task<ArtifactResponse> UploadArtifactAsync(
        Guid tenantId,
        Guid projectId,
        string key,
        string fileName,
        string mediaType,
        string classification,
        Guid creator,
        string content)
    {
        using var form = new MultipartFormDataContent();

        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        form.Add(file, "file", fileName);

        form.Add(new StringContent(key), "key");
        form.Add(new StringContent(fileName), "fileName");
        form.Add(new StringContent(mediaType), "mediaType");
        form.Add(new StringContent(classification), "classification");
        form.Add(new StringContent(creator.ToString()), "creator");

        using var client = _factory.CreateAuthenticatedHumanClient(tenantId, creator);
        using var response = await client.PostAsync($"/tenants/{tenantId}/projects/{projectId}/artifacts", form);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ArtifactResponse>(JsonOptions))!;
    }

    private async Task<List<ArtifactResponse>> ListArtifactsAsync(Guid tenantId, Guid projectId)
    {
        using var response = await _client.GetAsync($"/tenants/{tenantId}/projects/{projectId}/artifacts");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<ArtifactResponse>>(JsonOptions))!;
    }

    private async Task<List<ArtifactResponse>> ListArtifactVersionsAsync(Guid tenantId, Guid projectId, string key)
    {
        using var response = await _client.GetAsync($"/tenants/{tenantId}/projects/{projectId}/artifacts/versions?key={Uri.EscapeDataString(key)}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<ArtifactResponse>>(JsonOptions))!;
    }

    private async Task<ArtifactResponse?> GetArtifactAsync(Guid tenantId, Guid artifactId)
    {
        using var response = await _client.GetAsync($"/tenants/{tenantId}/artifacts/{artifactId}");
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ArtifactResponse>(JsonOptions);
    }

    private async Task<EvidenceResponse> RecordEvidenceAsync(
        Guid tenantId,
        Guid claimId,
        string type,
        string? integrityHash,
        Guid? relatedArtifactId,
        string relatedCriterion)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/claims/{claimId}/evidence",
            new RecordEvidenceRequest(type, integrityHash, relatedArtifactId, relatedCriterion),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EvidenceResponse>(JsonOptions))!;
    }

    private async Task<List<EvidenceResponse>> ListEvidenceForClaimAsync(Guid tenantId, Guid claimId)
    {
        using var response = await _client.GetAsync($"/tenants/{tenantId}/claims/{claimId}/evidence");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<EvidenceResponse>>(JsonOptions))!;
    }

    private async Task<List<EvidenceResponse>> ListEvidenceForProjectAsync(Guid tenantId, Guid projectId)
    {
        using var response = await _client.GetAsync($"/tenants/{tenantId}/projects/{projectId}/evidence");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<EvidenceResponse>>(JsonOptions))!;
    }
}
