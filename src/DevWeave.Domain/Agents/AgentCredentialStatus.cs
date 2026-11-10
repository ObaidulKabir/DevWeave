namespace DevWeave.Domain.Agents;

/// <summary>
/// Lifecycle of an agent credential (SRS AGT-002). A credential is issued Active, can be temporarily
/// Disabled (and re-enabled), and can be Revoked, which is terminal. Expiry is time-based on
/// <see cref="AgentCredential.ExpiresAt"/> rather than a distinct status.
/// </summary>
public enum AgentCredentialStatus
{
    Active = 1,
    Revoked = 2,
    Disabled = 3,
}