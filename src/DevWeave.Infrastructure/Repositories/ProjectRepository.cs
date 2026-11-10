using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevWeave.Infrastructure.Repositories;

internal sealed class ProjectRepository : IProjectRepository
{
    private readonly DevWeaveDbContext _db;

    public ProjectRepository(DevWeaveDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(Project project, CancellationToken ct = default)
    {
        _db.Projects.Add(project);
        return Task.CompletedTask;
    }

    // Scoped read: owned tenant + non-deleted. A deleted or cross-tenant project is indistinguishable
    // from a missing one (deny-by-default).
    public Task<Project?> GetByIdAsync(TenantId tenantId, ProjectId id, CancellationToken ct = default) =>
        _db.Projects.SingleOrDefaultAsync(p => p.TenantId == tenantId && p.Id == id && !p.IsDeleted, ct);

    public async Task<IReadOnlyList<Project>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default) =>
        await _db.Projects
            .Where(p => p.TenantId == tenantId && !p.IsDeleted)
            .ToListAsync(ct);

    // Key uniqueness is enforced over the whole tenant, including soft-deleted rows, to match the
    // database unique index (a key can never be reused once assigned).
    public Task<bool> KeyExistsAsync(TenantId tenantId, string key, CancellationToken ct = default) =>
        _db.Projects.AnyAsync(p => p.TenantId == tenantId && p.Key == key, ct);
}