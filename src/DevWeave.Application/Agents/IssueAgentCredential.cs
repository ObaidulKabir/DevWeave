using DevWeave.Application.Abstractions;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;

namespace DevWeave.Application.Agents;

/// <summary>
/// Issues a credential to an AI agent principal (SRS AGT-002). The secret is generated, hashed via
/// <see cref="ISecretHasher"/>, and returned in plaintext exactly once; only the hash is persisted.
/// </summary>
public sealed class IssueAgentCredential
{
    private readonly IAgentCredentialRepository _credentials;
    private readonly IPrincipalRepository _principals;
    private readonly ISecretHasher _hasher;
    private readonly IUnitOfWork _unitOfWork;

    public IssueAgentCredential(
        IAgentCredentialRepository credentials,
        IPrincipalRepository principals,
        ISecretHasher hasher,
        IUnitOfWork unitOfWork)
    {
        _credentials = credentials;
        _principals = principals;
        _hasher = hasher;
        _unitOfWork = unitOfWork;
    }

    public async Task<AgentCredentialIssued> ExecuteAsync(
        TenantId tenantId,
        PrincipalId agentId,
        string label,
        DateTimeOffset? expiresAt,
        CancellationToken ct = default)
    {
        var agent = await _principals.GetByIdAsync(tenantId, agentId, ct)
            ?? throw new NotFoundException($"Principal '{agentId}' was not found within tenant '{tenantId}'.");

        if (agent.Type != PrincipalType.AiAgent)
            throw new ConflictException($"Principal '{agentId}' is not an AI agent.");

        // Local-dev placeholder (AGT-002): a Guid-composed secret is hashed before storage. Full secret
        // management and HSM-backed crypto are deferred to a later slice.
        var plainText = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var hash = _hasher.Hash(plainText);

        var credential = AgentCredential.Issue(tenantId, agentId, label, hash, expiresAt);

        await _credentials.AddAsync(credential, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new AgentCredentialIssued(credential, plainText);
    }
}