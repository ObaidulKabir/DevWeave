using DevWeave.Domain.Identity;
using DevWeave.Domain.Lifecycle;

namespace DevWeave.Application.Abstractions;

public interface ILifecycleProfileRepository
{
    Task AddAsync(LifecycleProfile profile, CancellationToken ct = default);

    Task<LifecycleProfile?> GetByIdAsync(TenantId tenantId, LifecycleProfileId id, CancellationToken ct = default);

    Task<IReadOnlyList<LifecycleProfile>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default);
}