using DevWeave.Domain.Identity;

namespace DevWeave.Application.Abstractions;

public interface IMembershipRepository
{
    Task AddAsync(Membership membership, CancellationToken ct = default);

    Task<Membership?> GetByIdAsync(TenantId tenantId, MembershipId id, CancellationToken ct = default);

    Task<IReadOnlyList<Membership>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default);

    Task<IReadOnlyList<Membership>> ListByPrincipalAsync(TenantId tenantId, PrincipalId principalId, CancellationToken ct = default);
}