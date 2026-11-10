using DevWeave.Domain.Identity;

namespace DevWeave.Application.Abstractions;

public interface ITenantRepository
{
    Task AddAsync(Tenant tenant, CancellationToken ct = default);

    Task<Tenant?> GetByIdAsync(TenantId id, CancellationToken ct = default);
}