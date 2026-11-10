using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;

namespace DevWeave.Application.Identity;

public sealed class InviteMember
{
    private readonly IPrincipalRepository _principals;
    private readonly IMembershipRepository _memberships;
    private readonly IUnitOfWork _unitOfWork;

    public InviteMember(IPrincipalRepository principals, IMembershipRepository memberships, IUnitOfWork unitOfWork)
    {
        _principals = principals;
        _memberships = memberships;
        _unitOfWork = unitOfWork;
    }

    public async Task<Membership> ExecuteAsync(TenantId tenantId, PrincipalId principalId, MembershipRole role, CancellationToken ct = default)
    {
        // The principal must be resolvable inside the tenant scope; a cross-tenant principal is
        // indistinguishable from a missing one (deny-by-default) and both are rejected here.
        var principal = await _principals.GetByIdAsync(tenantId, principalId, ct)
            ?? throw new NotFoundException($"Principal '{principalId}' was not found within tenant '{tenantId}'.");

        var membership = Membership.Invite(tenantId, principal.Id, role);

        await _memberships.AddAsync(membership, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return membership;
    }
}