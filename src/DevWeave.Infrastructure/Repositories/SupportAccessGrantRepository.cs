using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevWeave.Infrastructure.Repositories;

internal sealed class SupportAccessGrantRepository : ISupportAccessGrantRepository
{
    private readonly DevWeaveDbContext _db;

    public SupportAccessGrantRepository(DevWeaveDbContext db) => _db = db;

    public async Task AddAsync(SupportAccessGrant grant, CancellationToken ct = default) =>
        await _db.SupportAccessGrants.AddAsync(grant, ct);

    public Task<SupportAccessGrant?> GetByIdAsync(
        TenantId tenantId,
        SupportAccessGrantId id,
        CancellationToken ct = default) =>
        _db.SupportAccessGrants.FirstOrDefaultAsync(
            grant => grant.TenantId == tenantId && grant.Id == id,
            ct);

    public async Task<SupportAccessGrant?> GetActiveAsync(
        TenantId tenantId,
        PrincipalId operatorId,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var grants = await _db.SupportAccessGrants
            .Where(grant =>
                grant.TenantId == tenantId &&
                grant.OperatorId == operatorId &&
                grant.RevokedAt == null &&
                grant.ExpiresAt > now)
            .OrderByDescending(grant => grant.ExpiresAt)
            .Take(1)
            .ToListAsync(ct);

        return grants.FirstOrDefault();
    }

    public async Task<IReadOnlyList<SupportAccessGrant>> ListByTenantAsync(
        TenantId tenantId,
        CancellationToken ct = default) =>
        await _db.SupportAccessGrants
            .Where(grant => grant.TenantId == tenantId)
            .OrderByDescending(grant => grant.CreatedAt)
            .ToListAsync(ct);
}
