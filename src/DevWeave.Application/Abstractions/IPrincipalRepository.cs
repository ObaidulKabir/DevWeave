using DevWeave.Domain.Identity;

namespace DevWeave.Application.Abstractions;

public interface IPrincipalRepository
{
    Task AddAsync(Principal principal, CancellationToken ct = default);

    Task<Principal?> GetByIdAsync(TenantId tenantId, PrincipalId id, CancellationToken ct = default);

    Task<IReadOnlyList<Principal>> ListByTenantAndTypeAsync(
        TenantId tenantId,
        PrincipalType type,
        CancellationToken ct = default);
}