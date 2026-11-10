using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;

namespace DevWeave.Application.Identity;

public sealed class ActivateMember
{
    private readonly IMembershipRepository _memberships;
    private readonly IUnitOfWork _unitOfWork;

    public ActivateMember(IMembershipRepository memberships, IUnitOfWork unitOfWork)
    {
        _memberships = memberships;
        _unitOfWork = unitOfWork;
    }

    public async Task<Membership> ExecuteAsync(TenantId tenantId, MembershipId membershipId, CancellationToken ct = default)
    {
        var membership = await _memberships.GetByIdAsync(tenantId, membershipId, ct)
            ?? throw new NotFoundException($"Membership '{membershipId}' was not found within tenant '{tenantId}'.");

        membership.Activate();

        await _unitOfWork.SaveChangesAsync(ct);

        return membership;
    }
}