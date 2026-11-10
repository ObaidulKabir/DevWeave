using System.Text;
using DevWeave.Application.Abstractions;
using DevWeave.Application.Artifacts;
using DevWeave.Domain.Artifacts;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Tests.Artifacts;

/// <summary>
/// Exercises the Artifact use cases (SRS ART-001..ART-004, ART-006, ART-008) against in-memory fakes,
/// focusing on flow correctness: upload-then-read, immutable version identity (ART-003), checksum
/// verification with tamper detection (ART-006), signed URL issuance (ART-004), and prohibited-content
/// rejection before persistence plus quarantine-on-ingest (ART-008).
/// </summary>
public sealed class ArtifactUseCaseTests
{
    private static readonly string[] QuarantineSourceCodeReasons = ["Source code with a prohibited extension was detected."];
    private static readonly string[] RejectSecretReasons = ["Secret marker 'AKIA' detected."];

    [Fact]
    public async Task ART_001_upload_get_and_list_round_trip_a_single_version()
    {
        var fakes = CreateFakes();
        var seed = await SeedAsync(fakes);

        var uploaded = await new UploadArtifact(
                fakes.Artifacts, fakes.Projects, fakes.Principals, fakes.Storage, fakes.Quarantine, fakes.Inspector, fakes.UnitOfWork)
            .ExecuteAsync(
                seed.Tenant, seed.Project, "coverage.pdf", "coverage.pdf", "application/pdf",
                Encoding.UTF8.GetBytes("hello world"), Classification.Internal, seed.Actor);

        Assert.Equal(seed.Tenant, uploaded.TenantId);
        Assert.Equal(seed.Project, uploaded.ProjectId);
        Assert.Equal(1, uploaded.Version);
        Assert.Equal("application/pdf", uploaded.MediaType);
        Assert.Equal(11, uploaded.SizeInBytes);
        Assert.False(string.IsNullOrWhiteSpace(uploaded.Checksum));
        Assert.Equal("sha256", uploaded.ChecksumAlgorithm);
        Assert.Equal(ArtifactStatus.Stored, uploaded.Status);

        var fetched = await new GetArtifact(fakes.Artifacts).ExecuteAsync(seed.Tenant, uploaded.Id);
        Assert.NotNull(fetched);
        Assert.Equal(uploaded.Id, fetched.Id);

        var listed = await new ListArtifactsForProject(fakes.Artifacts).ExecuteAsync(seed.Tenant, seed.Project);
        Assert.Single(listed);
        Assert.Equal(uploaded.Id, listed[0].Id);
    }

    [Fact]
    public async Task ART_003_replacement_preserves_the_prior_version_and_advances_the_number()
    {
        var fakes = CreateFakes();
        var seed = await SeedAsync(fakes);
        var upload = new UploadArtifact(
            fakes.Artifacts, fakes.Projects, fakes.Principals, fakes.Storage, fakes.Quarantine, fakes.Inspector, fakes.UnitOfWork);

        var first = await upload.ExecuteAsync(
            seed.Tenant, seed.Project, "coverage.pdf", "coverage.pdf", "application/pdf",
            Encoding.UTF8.GetBytes("v1"), Classification.Internal, seed.Actor);
        var second = await upload.ExecuteAsync(
            seed.Tenant, seed.Project, "coverage.pdf", "coverage.pdf", "application/pdf",
            Encoding.UTF8.GetBytes("v2"), Classification.Internal, seed.Actor);

        Assert.Equal(1, first.Version);
        Assert.Equal(2, second.Version);
        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(first.ObjectKey, second.ObjectKey);

        var versions = await new ListArtifactVersions(fakes.Artifacts).ExecuteAsync(seed.Tenant, seed.Project, "coverage.pdf");
        Assert.Equal(2, versions.Count);
        Assert.Contains(versions, a => a.Id == first.Id && a.Version == 1);
        Assert.Contains(versions, a => a.Id == second.Id && a.Version == 2);

        // Prior versions are preserved and still independently readable.
        Assert.NotNull(await new GetArtifact(fakes.Artifacts).ExecuteAsync(seed.Tenant, first.Id));
    }

    [Fact]
    public async Task ART_003_normalized_logical_keys_cannot_overwrite_each_others_objects()
    {
        var fakes = CreateFakes();
        var seed = await SeedAsync(fakes);
        var upload = new UploadArtifact(
            fakes.Artifacts, fakes.Projects, fakes.Principals, fakes.Storage, fakes.Quarantine, fakes.Inspector, fakes.UnitOfWork);

        var design = await upload.ExecuteAsync(seed.Tenant, seed.Project, "Design", "design.txt", "text/plain",
            Encoding.UTF8.GetBytes("upper-case key"), Classification.Internal, seed.Actor);
        var lowercaseDesign = await upload.ExecuteAsync(seed.Tenant, seed.Project, "design", "design.txt", "text/plain",
            Encoding.UTF8.GetBytes("lower-case key"), Classification.Internal, seed.Actor);

        Assert.Equal(1, design.Version);
        Assert.Equal(1, lowercaseDesign.Version);
        Assert.NotEqual(design.ObjectKey, lowercaseDesign.ObjectKey);
        Assert.Equal("upper-case key", Encoding.UTF8.GetString(fakes.Storage.Objects[design.ObjectKey]));
        Assert.Equal("lower-case key", Encoding.UTF8.GetString(fakes.Storage.Objects[lowercaseDesign.ObjectKey]));
    }

    [Fact]
    public async Task ART_003_failed_metadata_commit_removes_the_unreferenced_object()
    {
        var fakes = CreateFakes();
        var seed = await SeedAsync(fakes);
        fakes.UnitOfWork.ExceptionToThrow = new ConflictException("concurrent version allocation");

        var upload = new UploadArtifact(
            fakes.Artifacts, fakes.Projects, fakes.Principals, fakes.Storage, fakes.Quarantine, fakes.Inspector, fakes.UnitOfWork);

        await Assert.ThrowsAsync<ConflictException>(() => upload.ExecuteAsync(
            seed.Tenant, seed.Project, "design", "design.txt", "text/plain",
            Encoding.UTF8.GetBytes("orphan candidate"), Classification.Internal, seed.Actor));

        Assert.Empty(fakes.Storage.Objects);
    }

    [Fact]
    public async Task ART_006_verify_detects_tampered_bytes()
    {
        var fakes = CreateFakes();
        var seed = await SeedAsync(fakes);
        var artifact = await new UploadArtifact(
                fakes.Artifacts, fakes.Projects, fakes.Principals, fakes.Storage, fakes.Quarantine, fakes.Inspector, fakes.UnitOfWork)
            .ExecuteAsync(
                seed.Tenant, seed.Project, "data.bin", "data.bin", "application/octet-stream",
                Encoding.UTF8.GetBytes("integrity-checked"), Classification.Internal, seed.Actor);

        var verify = new VerifyArtifact(fakes.Artifacts, fakes.Storage);

        var intact = await verify.ExecuteAsync(seed.Tenant, artifact.Id);
        Assert.True(intact.Match);
        Assert.Equal(artifact.Checksum, intact.ActualChecksum);
        Assert.Equal("sha256", intact.ChecksumAlgorithm);

        // Recompute over mutated storage bytes and confirm the mismatch is surfaced.
        fakes.Storage.Objects[artifact.ObjectKey] = Encoding.UTF8.GetBytes("tampered");
        var tampered = await verify.ExecuteAsync(seed.Tenant, artifact.Id);
        Assert.False(tampered.Match);
        Assert.Equal(artifact.Checksum, tampered.ExpectedChecksum);
        Assert.NotEqual(tampered.ExpectedChecksum, tampered.ActualChecksum);
    }

    [Fact]
    public async Task ART_004_download_issues_a_presigned_url_and_bar_quarantined_content()
    {
        var fakes = CreateFakes();
        var seed = await SeedAsync(fakes);
        var artifact = await new UploadArtifact(
                fakes.Artifacts, fakes.Projects, fakes.Principals, fakes.Storage, fakes.Quarantine, fakes.Inspector, fakes.UnitOfWork)
            .ExecuteAsync(
                seed.Tenant, seed.Project, "design.pdf", "design.pdf", "application/pdf",
                Encoding.UTF8.GetBytes("pdf bytes"), Classification.Confidential, seed.Actor);

        var getUrl = new GetArtifactDownloadUrl(fakes.Artifacts, fakes.Storage);

        var download = await getUrl.ExecuteAsync(seed.Tenant, artifact.Id);
        Assert.False(string.IsNullOrWhiteSpace(download.Url));
        Assert.Contains("X-Amz-", download.Url);
        Assert.True(download.ExpiresAt > DateTimeOffset.UtcNow);
        Assert.True(
            download.ExpiresAt - DateTimeOffset.UtcNow <= GetArtifactDownloadUrl.DefaultExpiry + TimeSpan.FromSeconds(5),
            "The signed grant must be short-lived, never a permanent public URL.");

        // Quarantined content is never served (ART-008), so no URL can be issued for it.
        fakes.Inspector.Result = ContentInspectionResult.Quarantine(QuarantineSourceCodeReasons);
        var quarantined = await Assert.ThrowsAsync<QuarantinedContentException>(() =>
            new UploadArtifact(
                    fakes.Artifacts, fakes.Projects, fakes.Principals, fakes.Storage, fakes.Quarantine, fakes.Inspector, fakes.UnitOfWork)
                .ExecuteAsync(
                    seed.Tenant, seed.Project, "app.py", "app.py", "text/x-python",
                    Encoding.UTF8.GetBytes("print('hi')"), Classification.Internal, seed.Actor));

        await Assert.ThrowsAsync<NotFoundException>(() => getUrl.ExecuteAsync(seed.Tenant, new ArtifactId(quarantined.ArtifactId)));
    }

    [Fact]
    public async Task ART_008_prohibited_content_is_rejected_before_any_persistence()
    {
        var fakes = CreateFakes();
        var seed = await SeedAsync(fakes);
        fakes.Inspector.Result = ContentInspectionResult.Reject(RejectSecretReasons);

        var upload = new UploadArtifact(
            fakes.Artifacts, fakes.Projects, fakes.Principals, fakes.Storage, fakes.Quarantine, fakes.Inspector, fakes.UnitOfWork);

        var ex = await Assert.ThrowsAsync<ProhibitedContentException>(() =>
            upload.ExecuteAsync(
                seed.Tenant, seed.Project, "creds.txt", "creds.txt", "text/plain",
                Encoding.UTF8.GetBytes("AKIAABCDEFGHIJKLMNOP"), Classification.Restricted, seed.Actor));

        Assert.Contains("AKIA", Assert.Single(ex.Reasons));

        Assert.Empty(fakes.Artifacts.Items);
        Assert.Empty(fakes.Storage.Objects);
    }

    [Fact]
    public async Task ART_008_quarantined_content_is_persisted_but_flagged_and_surfaced()
    {
        var fakes = CreateFakes();
        var seed = await SeedAsync(fakes);
        fakes.Inspector.Result = ContentInspectionResult.Quarantine(QuarantineSourceCodeReasons);

        var upload = new UploadArtifact(
            fakes.Artifacts, fakes.Projects, fakes.Principals, fakes.Storage, fakes.Quarantine, fakes.Inspector, fakes.UnitOfWork);

        var ex = await Assert.ThrowsAsync<QuarantinedContentException>(() =>
            upload.ExecuteAsync(
                seed.Tenant, seed.Project, "Program.cs", "Program.cs", "text/plain",
                Encoding.UTF8.GetBytes("class Program {}"), Classification.Internal, seed.Actor));

        var quarantined = Assert.Single(fakes.Artifacts.Items);
        Assert.Equal(ArtifactStatus.Quarantined, quarantined.Status);
        Assert.Equal(quarantined.Id.Value, ex.ArtifactId);
        Assert.Equal(quarantined.ObjectKey, ex.ObjectKey);

        // Quarantined bytes never touch canonical storage; they live only in the isolated store.
        Assert.Empty(fakes.Storage.Objects);
        Assert.Single(fakes.Quarantine.Objects);
        Assert.Contains(quarantined.ObjectKey, fakes.Quarantine.Objects.Keys);
        Assert.Contains("quarantine", quarantined.ObjectKey);
    }

    [Fact]
    public async Task ART_008_purge_removes_only_expired_quarantine_objects()
    {
        var fakes = CreateFakes();
        var seed = await SeedAsync(fakes);
        fakes.Inspector.Result = ContentInspectionResult.Quarantine(QuarantineSourceCodeReasons);
        var upload = new UploadArtifact(
            fakes.Artifacts, fakes.Projects, fakes.Principals, fakes.Storage, fakes.Quarantine, fakes.Inspector, fakes.UnitOfWork);

        await Assert.ThrowsAsync<QuarantinedContentException>(() =>
            upload.ExecuteAsync(
                seed.Tenant, seed.Project, "Program.cs", "Program.cs", "text/plain",
                Encoding.UTF8.GetBytes("class Program {}"), Classification.Internal, seed.Actor));

        // A freshly quarantined object is still within its bounded lifetime, so nothing is purged yet.
        var purge = new PurgeExpiredQuarantine(fakes.Quarantine);
        Assert.Equal(0, await purge.ExecuteAsync(DateTimeOffset.UtcNow));
        Assert.Single(fakes.Quarantine.Objects);

        // Once the bounded lifetime elapses the bytes are deleted explicitly, so quarantine never
        // becomes permanent source retention.
        var afterLifetime = DateTimeOffset.UtcNow.Add(UploadArtifact.QuarantineLifetime).AddMinutes(1);
        Assert.Equal(1, await purge.ExecuteAsync(afterLifetime));
        Assert.Empty(fakes.Quarantine.Objects);

        // Re-running the sweep is idempotent.
        Assert.Equal(0, await purge.ExecuteAsync(afterLifetime));
    }

    private static Fakes CreateFakes() => new();

    private static async Task<(TenantId Tenant, ProjectId Project, PrincipalId Actor)> SeedAsync(Fakes fakes)
    {
        var tenant = Tenant.Create("Alpha Corp");
        var principal = Principal.Create(tenant.Id, "a@alphacorp.com", PrincipalType.Human);
        await fakes.Principals.AddAsync(principal);

        var project = Project.Create(tenant.Id, "Shop API", "shop-api", null, LifecycleProfileId.New(), principal.Id);
        await fakes.Projects.AddAsync(project);

        return (tenant.Id, project.Id, principal.Id);
    }

    private sealed class Fakes
    {
        public InMemoryArtifactRepository Artifacts { get; } = new();
        public InMemoryProjectRepository Projects { get; } = new();
        public InMemoryPrincipalRepository Principals { get; } = new();
        public InMemoryObjectStorage Storage { get; } = new();
        public InMemoryQuarantineStore Quarantine { get; } = new();
        public StubContentInspector Inspector { get; } = new();
        public InMemoryUnitOfWork UnitOfWork { get; } = new();
    }

    private sealed class InMemoryArtifactRepository : IArtifactRepository
    {
        public List<Artifact> Items { get; } = new();

        public Task AddAsync(Artifact artifact, CancellationToken ct = default)
        {
            Items.Add(artifact);
            return Task.CompletedTask;
        }

        public Task<Artifact?> GetByIdAsync(TenantId tenantId, ArtifactId id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(a => a.TenantId == tenantId && a.Id == id));

        public Task<IReadOnlyList<Artifact>> ListForProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Artifact>>(
                Items.Where(a => a.TenantId == tenantId && a.ProjectId == projectId)
                    .OrderBy(a => a.CreatedAt)
                    .ThenBy(a => a.Version)
                    .ToList());

        public Task<IReadOnlyList<Artifact>> ListVersionsAsync(TenantId tenantId, ProjectId projectId, string key, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Artifact>>(
                Items.Where(a => a.TenantId == tenantId && a.ProjectId == projectId && a.Key == key)
                    .OrderBy(a => a.Version)
                    .ToList());
    }

    private sealed class InMemoryProjectRepository : IProjectRepository
    {
        public List<Project> Projects { get; } = new();

        public Task AddAsync(Project project, CancellationToken ct = default)
        {
            Projects.Add(project);
            return Task.CompletedTask;
        }

        public Task<Project?> GetByIdAsync(TenantId tenantId, ProjectId id, CancellationToken ct = default) =>
            Task.FromResult(Projects.FirstOrDefault(p => p.TenantId == tenantId && p.Id == id && !p.IsDeleted));

        public Task<IReadOnlyList<Project>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Project>>(Projects.Where(p => p.TenantId == tenantId && !p.IsDeleted).ToList());

        public Task<bool> KeyExistsAsync(TenantId tenantId, string key, CancellationToken ct = default) =>
            Task.FromResult(Projects.Any(p => p.TenantId == tenantId && p.Key == key));
    }

    private sealed class InMemoryPrincipalRepository : IPrincipalRepository
    {
        public List<Principal> Principals { get; } = new();

        public Task AddAsync(Principal principal, CancellationToken ct = default)
        {
            Principals.Add(principal);
            return Task.CompletedTask;
        }

        public Task<Principal?> GetByIdAsync(TenantId tenantId, PrincipalId id, CancellationToken ct = default) =>
            Task.FromResult(Principals.FirstOrDefault(p => p.TenantId == tenantId && p.Id == id));
    }

    private sealed class InMemoryObjectStorage : IObjectStorage
    {
        public Dictionary<string, byte[]> Objects { get; } = new(StringComparer.Ordinal);

        public Task PutAsync(string key, byte[] content, string contentType, CancellationToken ct = default)
        {
            Objects[key] = content.ToArray();
            return Task.CompletedTask;
        }

        public Task<Stream> GetAsync(string key, CancellationToken ct = default)
        {
            var data = Objects[key];
            return Task.FromResult<Stream>(new MemoryStream(data, writable: false));
        }

        public Task<string> CreatePresignedGetUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default) =>
            Task.FromResult($"https://s3.example.test/{key}?X-Amz-Expires={(long)expiry.TotalSeconds}&X-Amz-Signature=test");

        public Task DeleteAsync(string key, CancellationToken ct = default)
        {
            Objects.Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class StubContentInspector : IContentInspector
    {
        public ContentInspectionResult Result { get; set; } = ContentInspectionResult.Allow();

        public Task<ContentInspectionResult> InspectAsync(
            string fileName,
            string mediaType,
            byte[] content,
            CancellationToken ct = default) =>
            Task.FromResult(Result);
    }

    private sealed class InMemoryQuarantineStore : IQuarantineStore
    {
        public Dictionary<string, (byte[] Content, DateTimeOffset ExpiresAt)> Objects { get; } = new(StringComparer.Ordinal);

        public Task<string> PutAsync(
            string artifactKey,
            byte[] content,
            string contentType,
            DateTimeOffset expiresAt,
            CancellationToken ct = default)
        {
            var key = $"quarantine/{expiresAt.ToUnixTimeSeconds()}/{Guid.NewGuid():N}";
            Objects[key] = (content.ToArray(), expiresAt);
            return Task.FromResult(key);
        }

        public Task<IReadOnlyList<string>> ListExpiredAsync(DateTimeOffset now, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>(
                Objects.Where(o => o.Value.ExpiresAt <= now).Select(o => o.Key).ToList());

        public Task DeleteAsync(string key, CancellationToken ct = default)
        {
            Objects.Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryUnitOfWork : IUnitOfWork
    {
        public Exception? ExceptionToThrow { get; set; }

        public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
            ExceptionToThrow is null
                ? Task.FromResult(0)
                : Task.FromException<int>(ExceptionToThrow);
    }
}
