using DevWeave.Application.Abstractions;
using DevWeave.Domain.Audit;
using DevWeave.Domain.Identity;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevWeave.Infrastructure.Repositories;

public sealed class AuditRepository : IAuditRepository
{
    private readonly DevWeaveDbContext _context;

    public AuditRepository(DevWeaveDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(AuditRecord record, CancellationToken ct = default)
    {
        await _context.AuditLogs.AddAsync(record, ct);
    }

    public async Task<AuditRecord?> GetByIdAsync(TenantId tenantId, AuditRecordId id, CancellationToken ct = default)
    {
        return await _context.AuditLogs
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == id, ct);
    }

    public async Task<List<AuditRecord>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default)
    {
        return await _context.AuditLogs
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.OccurredAt)
            .ToListAsync(ct);
    }

    public async Task<List<AuditRecord>> ListForResourceAsync(
        TenantId tenantId, string resourceType, string resourceId, CancellationToken ct = default)
    {
        return await _context.AuditLogs
            .Where(a => a.TenantId == tenantId && a.ResourceType == resourceType && a.ResourceId == resourceId)
            .OrderBy(a => a.OccurredAt)
            .ToListAsync(ct);
    }
}
