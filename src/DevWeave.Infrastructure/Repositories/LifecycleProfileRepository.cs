using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Lifecycle;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevWeave.Infrastructure.Repositories;

internal sealed class LifecycleProfileRepository : ILifecycleProfileRepository
{
    private readonly DevWeaveDbContext _db;

    public LifecycleProfileRepository(DevWeaveDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(LifecycleProfile profile, CancellationToken ct = default)
    {
        _db.LifecycleProfiles.Add(profile);
        return Task.CompletedTask;
    }

    // Scoped read: a cross-tenant profile is indistinguishable from a missing one.
    public Task<LifecycleProfile?> GetByIdAsync(TenantId tenantId, LifecycleProfileId id, CancellationToken ct = default) =>
        _db.LifecycleProfiles.SingleOrDefaultAsync(p => p.TenantId == tenantId && p.Id == id, ct);

    public async Task<IReadOnlyList<LifecycleProfile>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default) =>
        await _db.LifecycleProfiles
            .Where(p => p.TenantId == tenantId)
            .ToListAsync(ct);
}