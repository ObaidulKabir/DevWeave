using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;

namespace DevWeave.Application.Identity;

public sealed class GetTenant
{
    private readonly ITenantRepository _tenants;

    public GetTenant(ITenantRepository tenants)
    {
        _tenants = tenants;
    }

    public Task<Tenant?> ExecuteAsync(TenantId tenantId, CancellationToken ct = default) =>
        _tenants.GetByIdAsync(tenantId, ct);
}