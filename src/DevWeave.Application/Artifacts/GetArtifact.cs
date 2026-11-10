using DevWeave.Application.Abstractions;
using DevWeave.Domain.Artifacts;
using DevWeave.Domain.Identity;

namespace DevWeave.Application.Artifacts;

/// <summary>Reads the canonical metadata for one artifact version by id, tenant-scoped (SRS ART-002).</summary>
public sealed class GetArtifact
{
    private readonly IArtifactRepository _artifacts;

    public GetArtifact(IArtifactRepository artifacts)
    {
        _artifacts = artifacts;
    }

    public Task<Artifact?> ExecuteAsync(TenantId tenantId, ArtifactId id, CancellationToken ct = default) =>
        _artifacts.GetByIdAsync(tenantId, id, ct);
}