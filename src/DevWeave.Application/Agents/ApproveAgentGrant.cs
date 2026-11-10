using DevWeave.Application.Abstractions;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;

namespace DevWeave.Application.Agents;

/// <summary>
/// Approves a proposed agent capability grant (SRS AGT-013). The approver must be a human principal with
/// an active tenant-level Owner or Admin membership; the grant records who approved it and when for audit.
/// </summary>
public sealed class ApproveAgentGrant
{
    private readonly IAgentGrantRepository _grants;
    private readonly IPrincipalRepository _principals;
    private readonly IMembershipRepository _memberships;
    private readonly IUnitOfWork _unitOfWork;

    public ApproveAgentGrant(
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
        PrincipalId approverId,
        CancellationToken ct = default)
    {
        var grant = await _grants.GetByIdAsync(tenantId, grantId, ct)
            ?? throw new NotFoundException($"Agent grant '{grantId}' was not found within tenant '{tenantId}'.");

        var approver = await _principals.GetByIdAsync(tenantId, approverId, ct)
            ?? throw new NotFoundException($"Principal '{approverId}' was not found within tenant '{tenantId}'.");

        // Human approval authority (AGT-013): only an active Owner/Admin may approve a grant. A machine
        // principal or a member without approval authority is denied without disclosing grant details.
        if (approver.Type != PrincipalType.Human)
            throw new UnauthorizedTenantException($"Principal '{approverId}' is not a human and cannot approve agent grants.");

        var memberships = await _memberships.ListByPrincipalAsync(tenantId, approverId, ct);
        var isAuthority = memberships.Any(m =>
            m.Status == MembershipStatus.Active && m.Role is MembershipRole.Owner or MembershipRole.Admin);

        if (!isAuthority)
            throw new UnauthorizedTenantException($"Principal '{approverId}' lacks an active Owner/Admin membership for tenant '{tenantId}'.");

        try
        {
            grant.Approve(approver.Id);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return grant;
    }
}