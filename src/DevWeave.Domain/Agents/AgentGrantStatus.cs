namespace DevWeave.Domain.Agents;

/// <summary>
/// Lifecycle of an agent capability grant (SRS AGT-002, AGT-013). A grant is proposed by a human,
/// approved through review, and can later be revoked. Revoked is terminal; expiry is time-based on
/// <see cref="AgentGrant.ExpiresAt"/> rather than a distinct status.
/// </summary>
public enum AgentGrantStatus
{
    Proposed = 1,
    Approved = 2,
    Revoked = 3,
}