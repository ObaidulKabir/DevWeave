using DevWeave.Application.Abstractions;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;

namespace DevWeave.Application.Agents;

/// <summary>
/// Authenticates an AI agent by verifying a presented credential secret (SRS AGT-002, TEN-007). Success
/// returns the authenticated agent principal so the caller can mint a short-lived access token. Failure
/// is indistinguishable across unknown principal, unknown credential, mismatched ownership, inactive
/// credential and mismatched secret.
/// </summary>
public sealed class AuthenticateAgent
{
    private readonly IPrincipalRepository _principals;
    private readonly IAgentCredentialRepository _credentials;
    private readonly ISecretHasher _hasher;

    public AuthenticateAgent(
        IPrincipalRepository principals,
        IAgentCredentialRepository credentials,
        ISecretHasher hasher)
    {
        _principals = principals;
        _credentials = credentials;
        _hasher = hasher;
    }

    public async Task<Principal> ExecuteAsync(
        TenantId tenantId,
        PrincipalId agentId,
        AgentCredentialId credentialId,
        string secret,
        CancellationToken ct = default)
    {
        var agent = await _principals.GetByIdAsync(tenantId, agentId, ct);
        if (agent is null || agent.Type != PrincipalType.AiAgent)
            throw new UnauthorizedTenantException("Invalid agent credentials.");

        var credential = await _credentials.GetByIdAsync(tenantId, credentialId, ct);
        if (credential is null || credential.AgentId != agentId)
            throw new UnauthorizedTenantException("Invalid agent credentials.");

        if (!credential.IsActive(DateTimeOffset.UtcNow))
            throw new UnauthorizedTenantException("Invalid agent credentials.");

        if (!_hasher.Verify(secret, credential.SecretHash))
            throw new UnauthorizedTenantException("Invalid agent credentials.");

        return agent;
    }
}