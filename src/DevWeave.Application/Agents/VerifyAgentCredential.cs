using DevWeave.Application.Abstractions;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;

namespace DevWeave.Application.Agents;

/// <summary>
/// Verifies a presented secret against a credential's stored hash in constant time (SRS AGT-002). The
/// credential must be Active and not expired. Returns <see langword="false"/> for unknown, expired,
/// disabled or revoked credentials, and for mismatched or blank secrets, without disclosing which.
/// </summary>
public sealed class VerifyAgentCredential
{
    private readonly IAgentCredentialRepository _credentials;
    private readonly ISecretHasher _hasher;

    public VerifyAgentCredential(
        IAgentCredentialRepository credentials,
        ISecretHasher hasher)
    {
        _credentials = credentials;
        _hasher = hasher;
    }

    public async Task<bool> ExecuteAsync(
        TenantId tenantId,
        AgentCredentialId credentialId,
        string secret,
        CancellationToken ct = default)
    {
        var credential = await _credentials.GetByIdAsync(tenantId, credentialId, ct);
        if (credential is null)
            return false;

        if (!credential.IsActive(DateTimeOffset.UtcNow))
            return false;

        return _hasher.Verify(secret, credential.SecretHash);
    }
}