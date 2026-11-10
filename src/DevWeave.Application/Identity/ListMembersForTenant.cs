using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;

namespace DevWeave.Application.Identity;

public sealed class ListMembersForTenant
{
    private readonly IMembershipRepository _memberships;

    public ListMembersForTenant(IMembershipRepository memberships)
    {
        _memberships = memberships;
    }

    public Task<IReadOnlyList<Membership>> ExecuteAsync(TenantId tenantId, CancellationToken ct = default) =>
        _memberships.ListByTenantAsync(tenantId, ct);
}