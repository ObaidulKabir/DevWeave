using DevWeave.Domain.Identity;
using DevWeave.Domain.Integrations;

namespace DevWeave.Application.Abstractions;

public interface IIntegrationRepository
{
    Task<Integration?> GetByIdAsync(IntegrationId id, CancellationToken ct = default);
    Task<IReadOnlyList<Integration>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default);
    Task AddAsync(Integration integration, CancellationToken ct = default);
}
