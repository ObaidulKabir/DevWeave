using DevWeave.Application.Abstractions;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;

namespace DevWeave.Application.Agents;

/// <summary>
/// Rotates an agent credential by issuing a replacement and revoking the outgoing credential atomically
/// (SRS AGT-002). Requires human Owner/Admin authority. The plaintext secret is returned exactly once.
/// </summary>
public sealed class RotateAgentCredential
{
    private readonly IAgentCredentialRepository _credentials;
    private readonly IPrincipalRepository _principals;
    private readonly IMembershipRepository _memberships;
    private readonly ISecretHasher _hasher;
    private readonly IUnitOfWork _unitOfWork;

    public RotateAgentCredential(
        IAgentCredentialRepository credentials,
        IPrincipalRepository principals,
        IMembershipRepository memberships,
        ISecretHasher hasher,
        IUnitOfWork unitOfWork)
    {
        _credentials = credentials;
        _principals = principals;
        _memberships = memberships;
        _hasher = hasher;
        _unitOfWork = unitOfWork;
    }

    public async Task<AgentCredentialIssued> ExecuteAsync(
        TenantId tenantId,
        PrincipalId agentId,
        AgentCredentialId credentialId,
        PrincipalId actorId,
        DateTimeOffset? expiresAt,
        CancellationToken ct = default)
    {
        await AgentAuthority.EnsureHumanAdminAsync(
            _principals, _memberships, tenantId, actorId, "rotate agent credentials", ct);

        var current = await _credentials.GetByIdAsync(tenantId, credentialId, ct)
            ?? throw new NotFoundException($"Agent credential '{credentialId}' was not found within tenant '{tenantId}'.");

        if (current.AgentId != agentId)
            throw new NotFoundException($"Agent credential '{credentialId}' does not belong to agent '{agentId}'.");

        try
        {
            current.Revoke();
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        var plainText = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var hash = _hasher.Hash(plainText);

        var replacement = AgentCredential.Issue(
            tenantId,
            agentId,
            current.Label,
            hash,
            expiresAt ?? current.ExpiresAt);

        await _credentials.AddAsync(replacement, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new AgentCredentialIssued(replacement, plainText);
    }
}