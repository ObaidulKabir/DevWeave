using DevWeave.Application.Abstractions;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;

namespace DevWeave.Application.Agents;

/// <summary>
/// Revokes an agent capability grant (SRS AGT-013). Revocation is terminal and audited with the
/// revoker's identity and timestamp. The revoker must be a human with an active Owner/Admin membership.
/// </summary>
public sealed class RevokeAgentGrant
{
    private readonly IAgentGrantRepository _grants;
    private readonly IPrincipalRepository _principals;
    private readonly IMembershipRepository _memberships;
    private readonly IUnitOfWork _unitOfWork;

    public RevokeAgentGrant(
        IAgentGrantRepository grants,
        IPrincipalRepository principals,
        IMembershipRepository memberships,
        IUnitOfWork unitOfWork)
    {
        _grants = grants;
        _principals = principals;
        _memberships = memberships;
        _unitOfWork = unitOfWork;
    }

    public async Task<AgentGrant> ExecuteAsync(
        TenantId tenantId,
        AgentGrantId grantId,
        PrincipalId revokerId,
        CancellationToken ct = default)
    {
        var grant = await _grants.GetByIdAsync(tenantId, grantId, ct)
            ?? throw new NotFoundException($"Agent grant '{grantId}' was not found within tenant '{tenantId}'.");

        var revoker = await _principals.GetByIdAsync(tenantId, revokerId, ct)
            ?? throw new NotFoundException($"Principal '{revokerId}' was not found within tenant '{tenantId}'.");

        if (revoker.Type != PrincipalType.Human)
            throw new UnauthorizedTenantException($"Principal '{revokerId}' is not a human and cannot revoke agent grants.");

        var memberships = await _memberships.ListByPrincipalAsync(tenantId, revokerId, ct);
        var isAuthority = memberships.Any(m =>
            m.Status == MembershipStatus.Active && m.Role is MembershipRole.Owner or MembershipRole.Admin);

        if (!isAuthority)
            throw new UnauthorizedTenantException($"Principal '{revokerId}' lacks an active Owner/Admin membership for tenant '{tenantId}'.");

        try
        {
            grant.Revoke(revoker.Id);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return grant;
    }
}