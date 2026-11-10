using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Lifecycle;

namespace DevWeave.Application.Lifecycle;

public sealed class ListLifecycleProfilesForTenant
{
    private readonly ILifecycleProfileRepository _profiles;

    public ListLifecycleProfilesForTenant(ILifecycleProfileRepository profiles)
    {
        _profiles = profiles;
    }

    public Task<IReadOnlyList<LifecycleProfile>> ExecuteAsync(TenantId tenantId, CancellationToken ct = default) =>
        _profiles.ListByTenantAsync(tenantId, ct);
}