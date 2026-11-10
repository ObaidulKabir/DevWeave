using DevWeave.Domain.Identity;

namespace DevWeave.Application.Abstractions;

/// <summary>
/// Issues and validates short-lived bearer access tokens that carry a verified tenant/actor identity
/// (SRS TEN-007, GOV-001). A token is minted after a principal authenticates — for agents, by presenting
/// an active credential secret — and is validated by the API authentication middleware to establish the
/// request principal without a per-call database credential lookup.
/// </summary>
public interface IAccessTokenService
{
    /// <summary>Mints a signed access token for the given subject with the requested lifetime.</summary>
    string Issue(TenantId tenantId, PrincipalId principalId, PrincipalType principalType, TimeSpan lifetime);

    /// <summary>
    /// Validates a token and returns its subject, or <see langword="null"/> when the token is malformed,
    /// tampered, or expired.
    /// </summary>
    AuthenticatedSubject? Validate(string token);
}

/// <summary>The verified identity produced from a valid access token.</summary>
public sealed record AuthenticatedSubject(
    TenantId TenantId,
    PrincipalId PrincipalId,
    PrincipalType PrincipalType);