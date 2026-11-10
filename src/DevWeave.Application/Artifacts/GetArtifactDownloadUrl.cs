using DevWeave.Application.Abstractions;
using DevWeave.Domain.Artifacts;
using DevWeave.Domain.Identity;

namespace DevWeave.Application.Artifacts;

/// <summary>
/// Issues a short-lived presigned download URL for an artifact (SRS ART-004). The caller must already
/// hold the tenant scope; a cross-tenant or missing artifact surfaces as <see cref="NotFoundException"/>.
/// Quarantined content is never served, so no URL is issued for it (SRS ART-008).
/// </summary>
public sealed class GetArtifactDownloadUrl
{
    public static readonly TimeSpan DefaultExpiry = TimeSpan.FromMinutes(15);

    private readonly IArtifactRepository _artifacts;
    private readonly IObjectStorage _storage;

    public GetArtifactDownloadUrl(IArtifactRepository artifacts, IObjectStorage storage)
    {
        _artifacts = artifacts;
        _storage = storage;
    }

    public async Task<ArtifactDownload> ExecuteAsync(
        TenantId tenantId,
        ArtifactId id,
        TimeSpan? expiry = null,
        CancellationToken ct = default)
    {
        var artifact = await _artifacts.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Artifact '{id}' was not found within tenant '{tenantId}'.");

        if (artifact.Status == ArtifactStatus.Quarantined)
            throw new NotFoundException($"Artifact '{id}' is quarantined and is not served.");

        var effectiveExpiry = expiry ?? DefaultExpiry;
        var url = await _storage.CreatePresignedGetUrlAsync(artifact.ObjectKey, effectiveExpiry, ct);

        return new ArtifactDownload(url, DateTimeOffset.UtcNow.Add(effectiveExpiry));
    }
}