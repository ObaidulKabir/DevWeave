using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevWeave.Infrastructure.Repositories;

internal sealed class MembershipRepository : IMembershipRepository
{
    private readonly DevWeaveDbContext _db;

    public MembershipRepository(DevWeaveDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(Membership membership, CancellationToken ct = default)
    {
        _db.Memberships.Add(membership);
        return Task.CompletedTask;
    }

    // Scoped read: the owning tenant is always part of the predicate.
    public Task<Membership?> GetByIdAsync(TenantId tenantId, MembershipId id, CancellationToken ct = default) =>
        _db.Memberships.SingleOrDefaultAsync(m => m.TenantId == tenantId && m.Id == id, ct);

    // Scoped read: only memberships owned by the supplied tenant are ever enumerated.
    public async Task<IReadOnlyList<Membership>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default) =>
        await _db.Memberships
            .Where(m => m.TenantId == tenantId)
            .ToListAsync(ct);

    // Scoped read: the principal's memberships within the tenant, insulated from cross-tenant rows.
    public async Task<IReadOnlyList<Membership>> ListByPrincipalAsync(TenantId tenantId, PrincipalId principalId, CancellationToken ct = default) =>
        await _db.Memberships
            .Where(m => m.TenantId == tenantId && m.PrincipalId == principalId)
            .ToListAsync(ct);
}