using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;

namespace DevWeave.Application.Identity;

public sealed class CreateTenant
{
    private readonly ITenantRepository _tenants;
    private readonly IUnitOfWork _unitOfWork;

    public CreateTenant(ITenantRepository tenants, IUnitOfWork unitOfWork)
    {
        _tenants = tenants;
        _unitOfWork = unitOfWork;
    }

    public async Task<Tenant> ExecuteAsync(string name, CancellationToken ct = default)
    {
        var tenant = Tenant.Create(name);

        await _tenants.AddAsync(tenant, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return tenant;
    }
}