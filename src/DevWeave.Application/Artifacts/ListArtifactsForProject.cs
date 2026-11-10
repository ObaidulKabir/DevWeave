using DevWeave.Application.Abstractions;
using DevWeave.Domain.Artifacts;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Artifacts;

/// <summary>Lists the artifact versions recorded for one project, tenant-scoped (deny-by-default, SRS ART-002).</summary>
public sealed class ListArtifactsForProject
{
    private readonly IArtifactRepository _artifacts;

    public ListArtifactsForProject(IArtifactRepository artifacts)
    {
        _artifacts = artifacts;
    }

    public Task<IReadOnlyList<Artifact>> ExecuteAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default) =>
        _artifacts.ListForProjectAsync(tenantId, projectId, ct);
}