using DevWeave.Domain.Audit;
using DevWeave.Domain.Identity;

namespace DevWeave.Application.Abstractions;

public interface IAuditRepository
{
    Task AddAsync(AuditRecord record, CancellationToken ct = default);
    Task<AuditRecord?> GetByIdAsync(TenantId tenantId, AuditRecordId id, CancellationToken ct = default);
    Task<List<AuditRecord>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default);

    /// <summary>Records about one resource, oldest first.</summary>
    Task<List<AuditRecord>> ListForResourceAsync(TenantId tenantId, string resourceType, string resourceId, CancellationToken ct = default);
}
