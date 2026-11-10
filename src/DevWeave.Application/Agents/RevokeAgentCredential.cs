using DevWeave.Application.Abstractions;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;

namespace DevWeave.Application.Agents;

/// <summary>
/// Revokes an agent credential, which is terminal (SRS AGT-002). Requires human Owner/Admin authority.
/// </summary>
public sealed class RevokeAgentCredential
{
    private readonly IAgentCredentialRepository _credentials;
    private readonly IPrincipalRepository _principals;
    private readonly IMembershipRepository _memberships;
    private readonly IUnitOfWork _unitOfWork;

    public RevokeAgentCredential(
        IAgentCredentialRepository credentials,
        IPrincipalRepository principals,
        IMembershipRepository memberships,
        IUnitOfWork unitOfWork)
    {
        _credentials = credentials;
        _principals = principals;
        _memberships = memberships;
        _unitOfWork = unitOfWork;
    }

    public async Task<AgentCredential> ExecuteAsync(
        TenantId tenantId,
        PrincipalId agentId,
        AgentCredentialId credentialId,
        PrincipalId actorId,
        CancellationToken ct = default)
    {
        await AgentAuthority.EnsureHumanAdminAsync(
            _principals, _memberships, tenantId, actorId, "revoke agent credentials", ct);

        var credential = await _credentials.GetByIdAsync(tenantId, credentialId, ct)
            ?? throw new NotFoundException($"Agent credential '{credentialId}' was not found within tenant '{tenantId}'.");

        if (credential.AgentId != agentId)
            throw new NotFoundException($"Agent credential '{credentialId}' does not belong to agent '{agentId}'.");

        try
        {
            credential.Revoke();
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return credential;
    }
}