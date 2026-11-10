using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevWeave.Infrastructure.Repositories;

internal sealed class TenantRepository : ITenantRepository
{
    private readonly DevWeaveDbContext _db;

    public TenantRepository(DevWeaveDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(Tenant tenant, CancellationToken ct = default)
    {
        _db.Tenants.Add(tenant);
        return Task.CompletedTask;
    }

    public Task<Tenant?> GetByIdAsync(TenantId id, CancellationToken ct = default) =>
        _db.Tenants.SingleOrDefaultAsync(t => t.Id == id, ct);
}