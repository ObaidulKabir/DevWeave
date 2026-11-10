using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;

namespace DevWeave.Application.Identity;

public sealed class CreatePrincipal
{
    private readonly IPrincipalRepository _principals;
    private readonly IUnitOfWork _unitOfWork;

    public CreatePrincipal(IPrincipalRepository principals, IUnitOfWork unitOfWork)
    {
        _principals = principals;
        _unitOfWork = unitOfWork;
    }

    public async Task<Principal> ExecuteAsync(TenantId tenantId, string email, PrincipalType type, CancellationToken ct = default)
    {
        var principal = Principal.Create(tenantId, email, type);

        await _principals.AddAsync(principal, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return principal;
    }
}