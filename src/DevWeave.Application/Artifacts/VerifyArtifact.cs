using DevWeave.Application.Abstractions;
using DevWeave.Domain.Artifacts;
using DevWeave.Domain.Identity;

namespace DevWeave.Application.Artifacts;

/// <summary>
/// Recomputes the SHA-256 checksum of an artifact's current object-storage bytes and compares it against
/// the checksum recorded at ingest (SRS ART-006). The checksum maths and the download/compare
/// orchestration stay here in the application layer; only byte access is delegated to <see cref="IObjectStorage"/>.
/// </summary>
public sealed class VerifyArtifact
{
    private readonly IArtifactRepository _artifacts;
    private readonly IObjectStorage _storage;

    public VerifyArtifact(IArtifactRepository artifacts, IObjectStorage storage)
    {
        _artifacts = artifacts;
        _storage = storage;
    }

    public async Task<ArtifactVerificationResult> ExecuteAsync(
        TenantId tenantId,
        ArtifactId id,
        CancellationToken ct = default)
    {
        var artifact = await _artifacts.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Artifact '{id}' was not found within tenant '{tenantId}'.");

        // Quarantined bytes live only in the isolated quarantine store and are not served, so there is
        // nothing to revalidate from canonical storage (SRS ART-008).
        if (artifact.Status == ArtifactStatus.Quarantined)
            throw new NotFoundException($"Artifact '{id}' is quarantined and is not served.");

        await using var stream = await _storage.GetAsync(artifact.ObjectKey, ct);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);

        var actual = Checksums.ComputeSha256(buffer.ToArray());

        return new ArtifactVerificationResult(
            artifact.Id.Value,
            artifact.Checksum,
            actual,
            artifact.ChecksumAlgorithm,
            string.Equals(actual, artifact.Checksum, StringComparison.Ordinal));
    }
}