using DevWeave.Domain.Artifacts;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Abstractions;

/// <summary>
/// Append-only artifact store port (SRS ART-003). Deliberately exposes only <c>Add</c> plus tenant-scoped
/// reads — there is no Update or Remove path, mirroring the immutable-version invariant of the domain.
/// </summary>
public interface IArtifactRepository
{
    Task AddAsync(Artifact artifact, CancellationToken ct = default);

    Task<Artifact?> GetByIdAsync(TenantId tenantId, ArtifactId id, CancellationToken ct = default);

    Task<IReadOnlyList<Artifact>> ListForProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default);

    Task<IReadOnlyList<Artifact>> ListVersionsAsync(TenantId tenantId, ProjectId projectId, string key, CancellationToken ct = default);

    Task<IReadOnlyList<Artifact>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default);
}