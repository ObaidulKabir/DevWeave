namespace DevWeave.Api.Security;

/// <summary>
/// Claim type constants emitted by the bearer-token authentication handler into the request principal
/// (SRS TEN-007). Authorization policies and minimal-API handlers read these to recover the verified
/// tenant/actor identity without trusting unverified route selectors.
/// </summary>
public static class AuthClaims
{
    public const string TenantId = "tenant_id";
    public const string PrincipalId = "principal_id";
    public const string PrincipalType = "principal_type";
}