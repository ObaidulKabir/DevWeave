using DevWeave.Domain.Identity;

namespace DevWeave.Application.Abstractions;

public interface ISupportAccessGrantRepository
{
    Task AddAsync(SupportAccessGrant grant, CancellationToken ct = default);
    Task<SupportAccessGrant?> GetByIdAsync(TenantId tenantId, SupportAccessGrantId id, CancellationToken ct = default);
    Task<SupportAccessGrant?> GetActiveAsync(TenantId tenantId, PrincipalId operatorId, DateTimeOffset now, CancellationToken ct = default);
    Task<IReadOnlyList<SupportAccessGrant>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default);
}
