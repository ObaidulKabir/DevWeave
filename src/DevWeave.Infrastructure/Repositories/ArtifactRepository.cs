using DevWeave.Application.Abstractions;
using DevWeave.Domain.Artifacts;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevWeave.Infrastructure.Repositories;

internal sealed class ArtifactRepository : IArtifactRepository
{
    private readonly DevWeaveDbContext _db;

    public ArtifactRepository(DevWeaveDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(Artifact artifact, CancellationToken ct = default)
    {
        _db.Artifacts.Add(artifact);
        return Task.CompletedTask;
    }

    // Deny-by-default: a cross-tenant artifact is indistinguishable from a missing one.
    public Task<Artifact?> GetByIdAsync(TenantId tenantId, ArtifactId id, CancellationToken ct = default) =>
        _db.Artifacts.SingleOrDefaultAsync(a => a.TenantId == tenantId && a.Id == id, ct);

    public async Task<IReadOnlyList<Artifact>> ListForProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default) =>
        await _db.Artifacts
            .Where(a => a.TenantId == tenantId && a.ProjectId == projectId)
            .OrderBy(a => a.CreatedAt)
            .ThenBy(a => a.Version)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Artifact>> ListVersionsAsync(TenantId tenantId, ProjectId projectId, string key, CancellationToken ct = default) =>
        await _db.Artifacts
            .Where(a => a.TenantId == tenantId && a.ProjectId == projectId && a.Key == key)
            .OrderBy(a => a.Version)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Artifact>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default) =>
        await _db.Artifacts
            .Where(a => a.TenantId == tenantId)
            .ToListAsync(ct);
}