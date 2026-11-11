using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DevWeave.Domain.Identity;
using Microsoft.IdentityModel.Tokens;

namespace DevWeave.Api.Security;

/// <summary>
/// Development-only human JWT mint used when OIDC authority is empty for local pilots.
/// Tokens use the same claim shape required by production OIDC (tenant_id, principal_id, principal_type).
/// </summary>
public sealed class PilotHumanTokenService
{
    public const string Issuer = "https://pilot.devweave.local";
    public const string Audience = "devweave-api";

    private readonly SymmetricSecurityKey _signingKey;
    private readonly TimeSpan _lifetime;

    public PilotHumanTokenService(string signingKey, TimeSpan? lifetime = null)
    {
        if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32)
            throw new InvalidOperationException("Pilot human token signing key must be at least 32 characters.");

        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        _lifetime = lifetime ?? TimeSpan.FromHours(8);
    }

    public string Issue(TenantId tenantId, PrincipalId principalId, bool platformAdmin = false)
    {
        var claims = new List<Claim>
        {
            new("sub", principalId.Value.ToString("N")),
            new(AuthClaims.TenantId, tenantId.Value.ToString("D")),
            new(AuthClaims.PrincipalId, principalId.Value.ToString("D")),
            new(AuthClaims.PrincipalType, "Human"),
        };

        if (platformAdmin)
            claims.Add(new Claim("platform_admin", "true"));

        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            Issuer,
            Audience,
            claims,
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.Add(_lifetime),
            credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
