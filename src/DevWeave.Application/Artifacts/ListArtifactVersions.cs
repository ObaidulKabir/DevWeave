using DevWeave.Application.Abstractions;
using DevWeave.Domain.Artifacts;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Artifacts;

/// <summary>Lists the immutable version history of one logical artifact key, tenant-scoped (SRS ART-003).</summary>
public sealed class ListArtifactVersions
{
    private readonly IArtifactRepository _artifacts;

    public ListArtifactVersions(IArtifactRepository artifacts)
    {
        _artifacts = artifacts;
    }

    public Task<IReadOnlyList<Artifact>> ExecuteAsync(
        TenantId tenantId,
        ProjectId projectId,
        string key,
        CancellationToken ct = default) =>
        _artifacts.ListVersionsAsync(tenantId, projectId, key, ct);
}