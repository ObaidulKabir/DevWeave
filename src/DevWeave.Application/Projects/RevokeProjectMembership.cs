using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Projects;

public sealed class RevokeProjectMembership
{
    private readonly IProjectMembershipRepository _memberships;
    private readonly IUnitOfWork _unitOfWork;

    public RevokeProjectMembership(IProjectMembershipRepository memberships, IUnitOfWork unitOfWork)
    {
        _memberships = memberships;
        _unitOfWork = unitOfWork;
    }

    public async Task<ProjectMembership> ExecuteAsync(
        TenantId tenantId,
        ProjectMembershipId membershipId,
        CancellationToken ct = default)
    {
        var membership = await _memberships.GetByIdAsync(tenantId, membershipId, ct)
            ?? throw new NotFoundException($"Project membership '{membershipId}' was not found within tenant '{tenantId}'.");

        membership.Revoke();

        await _unitOfWork.SaveChangesAsync(ct);

        return membership;
    }
}