using DevWeave.Application.Abstractions;
using DevWeave.Domain.Artifacts;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Artifacts;

/// <summary>
/// Ingests a binary artifact: validates metadata, enforces a size guard, computes the SHA-256 checksum
/// (ART-006), runs the content inspector (ART-008), derives a collision-safe object key and creates an
/// immutable version record (ART-003). Prohibited content is rejected before persistence; quarantined
/// content is written to the isolated, bounded-lifetime quarantine store with a
/// <see cref="ArtifactStatus.Quarantined"/> disposition and surfaced as a 422 quarantine notice, never
/// to canonical storage (SRS 7.7, AC-09).
/// </summary>
public sealed class UploadArtifact
{
    public const long MaxArtifactSizeInBytes = 64L * 1024 * 1024;

    /// <summary>The bounded quarantine lifetime before the retention sweep removes flagged bytes (ART-008).</summary>
    public static readonly TimeSpan QuarantineLifetime = TimeSpan.FromDays(7);

    private readonly IArtifactRepository _artifacts;
    private readonly IProjectRepository _projects;
    private readonly IPrincipalRepository _principals;
    private readonly IObjectStorage _storage;
    private readonly IQuarantineStore _quarantine;
    private readonly IContentInspector _inspector;
    private readonly IUnitOfWork _unitOfWork;

    public UploadArtifact(
        IArtifactRepository artifacts,
        IProjectRepository projects,
        IPrincipalRepository principals,
        IObjectStorage storage,
        IQuarantineStore quarantine,
        IContentInspector inspector,
        IUnitOfWork unitOfWork)
    {
        _artifacts = artifacts;
        _projects = projects;
        _principals = principals;
        _storage = storage;
        _quarantine = quarantine;
        _inspector = inspector;
        _unitOfWork = unitOfWork;
    }

    public async Task<Artifact> ExecuteAsync(
        TenantId tenantId,
        ProjectId projectId,
        string key,
        string fileName,
        string mediaType,
        byte[] content,
        Classification classification,
        PrincipalId creator,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (content.Length > MaxArtifactSizeInBytes)
            throw new ArgumentException($"Artifact exceeds the maximum size of {MaxArtifactSizeInBytes} bytes.", nameof(content));

        // Deny-by-default: a cross-tenant project or actor is indistinguishable from a missing one.
        _ = await _projects.GetByIdAsync(tenantId, projectId, ct)
            ?? throw new NotFoundException($"Project '{projectId}' was not found within tenant '{tenantId}'.");
        _ = await _principals.GetByIdAsync(tenantId, creator, ct)
            ?? throw new NotFoundException($"Principal '{creator}' was not found within tenant '{tenantId}'.");

        var checksum = Checksums.ComputeSha256(content);

        var inspection = await _inspector.InspectAsync(fileName, mediaType, content, ct);

        // ART-008: high-signal prohibited content is rejected before any byte or metadata is persisted.
        if (inspection.Verdict == ContentVerdict.Reject)
            throw new ProhibitedContentException(
                $"Artifact '{key}' was rejected by the content inspector.", inspection.Reasons);

        var version = await NextVersionAsync(tenantId, projectId, key, ct);

        // ART-008: quarantined bytes never touch canonical storage. They go to the isolated
        // bounded-lifetime store, so flagged content stays reviewable but is never served, indexed or
        // exported — and cannot become permanent source retention (SRS 7.7, AC-09).
        if (inspection.Verdict == ContentVerdict.Quarantine)
        {
            var quarantineKey = await _quarantine.PutAsync(
                key, content, mediaType, DateTimeOffset.UtcNow.Add(QuarantineLifetime), ct);

            var quarantined = Artifact.Create(
                tenantId,
                projectId,
                key,
                version,
                mediaType,
                content.Length,
                checksum,
                "sha256",
                quarantineKey,
                classification,
                creator,
                ArtifactStatus.Quarantined);

            await _artifacts.AddAsync(quarantined, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            throw new QuarantinedContentException(
                $"Artifact '{key}' was quarantined pending review.",
                quarantined.Id.Value,
                quarantineKey,
                inspection.Reasons);
        }

        var objectKey = BuildObjectKey(tenantId, projectId);

        var artifact = Artifact.Create(
            tenantId,
            projectId,
            key,
            version,
            mediaType,
            content.Length,
            checksum,
            "sha256",
            objectKey,
            classification,
            creator,
            ArtifactStatus.Stored);

        await _storage.PutAsync(objectKey, content, mediaType, ct);
        try
        {
            await _artifacts.AddAsync(artifact, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch
        {
            try
            {
                await _storage.DeleteAsync(objectKey, CancellationToken.None);
            }
            catch
            {
                // Preserve the original persistence failure; orphan cleanup is retried operationally.
            }

            throw;
        }

        return artifact;
    }

    private async Task<int> NextVersionAsync(TenantId tenantId, ProjectId projectId, string key, CancellationToken ct)
    {
        var versions = await _artifacts.ListVersionsAsync(tenantId, projectId, key, ct);
        return versions.Count == 0 ? 1 : versions.Max(v => v.Version) + 1;
    }

    private static string BuildObjectKey(TenantId tenantId, ProjectId projectId) =>
        $"{tenantId.Value:N}/{projectId.Value:N}/artifacts/{Guid.NewGuid():N}";
}
