using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevWeave.Infrastructure.Repositories;

internal sealed class PrincipalRepository : IPrincipalRepository
{
    private readonly DevWeaveDbContext _db;

    public PrincipalRepository(DevWeaveDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(Principal principal, CancellationToken ct = default)
    {
        _db.Principals.Add(principal);
        return Task.CompletedTask;
    }

    // Scoped read: the owning tenant is always part of the predicate, so a principal from
    // another tenant is indistinguishable from a missing one.
    public Task<Principal?> GetByIdAsync(TenantId tenantId, PrincipalId id, CancellationToken ct = default) =>
        _db.Principals.SingleOrDefaultAsync(p => p.TenantId == tenantId && p.Id == id, ct);

    public async Task<IReadOnlyList<Principal>> ListByTenantAndTypeAsync(
        TenantId tenantId,
        PrincipalType type,
        CancellationToken ct = default) =>
        await _db.Principals
            .Where(p => p.TenantId == tenantId && p.Type == type)
            .OrderBy(p => p.Email)
            .ToListAsync(ct);
}