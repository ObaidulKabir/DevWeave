using DevWeave.Domain.Identity;

namespace DevWeave.Domain.Agents;

/// <summary>
/// A credential issued to an AI agent principal (SRS AGT-002). Only the hash of the secret is stored;
/// the plaintext is returned exactly once at issuance time. The credential carries an optional expiry,
/// can be temporarily Disabled (and re-enabled), and can be Revoked, which is terminal.
/// </summary>
public sealed class AgentCredential
{
    public AgentCredentialId Id { get; }
    public TenantId TenantId { get; }
    public PrincipalId AgentId { get; }
    public string Label { get; }
    public string SecretHash { get; }
    public AgentCredentialStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? ExpiresAt { get; }
    public DateTimeOffset? RevokedAt { get; private set; }

    private AgentCredential(
        AgentCredentialId id,
        TenantId tenantId,
        PrincipalId agentId,
        string label,
        string secretHash,
        AgentCredentialStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset? expiresAt)
    {
        Id = id;
        TenantId = tenantId;
        AgentId = agentId;
        Label = label;
        SecretHash = secretHash;
        Status = status;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public static AgentCredential Issue(
        TenantId tenantId,
        PrincipalId agentId,
        string label,
        string secretHash,
        DateTimeOffset? expiresAt)
    {
        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Credential label is required.", nameof(label));

        if (string.IsNullOrWhiteSpace(secretHash))
            throw new ArgumentException("Credential secret hash is required.", nameof(secretHash));

        return new AgentCredential(
            AgentCredentialId.New(),
            tenantId,
            agentId,
            label,
            secretHash,
            AgentCredentialStatus.Active,
            DateTimeOffset.UtcNow,
            expiresAt);
    }

    public void Revoke()
    {
        if (Status == AgentCredentialStatus.Revoked)
            throw new InvalidOperationException("Agent credential is already revoked.");

        Status = AgentCredentialStatus.Revoked;
        RevokedAt = DateTimeOffset.UtcNow;
    }

    public void Disable()
    {
        if (Status == AgentCredentialStatus.Revoked)
            throw new InvalidOperationException("A revoked agent credential cannot be disabled.");

        if (Status == AgentCredentialStatus.Disabled)
            throw new InvalidOperationException("Agent credential is already disabled.");

        Status = AgentCredentialStatus.Disabled;
    }

    public void Enable()
    {
        if (Status != AgentCredentialStatus.Disabled)
            throw new InvalidOperationException("Agent credential is not disabled.");

        Status = AgentCredentialStatus.Active;
    }

    /// <summary>A credential is usable only while Active and not past its expiry (AGT-002).</summary>
    public bool IsActive(DateTimeOffset at) =>
        Status == AgentCredentialStatus.Active && (ExpiresAt is null || at < ExpiresAt);
}