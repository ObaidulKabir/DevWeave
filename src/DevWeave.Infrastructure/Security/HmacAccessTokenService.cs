using System.Security.Cryptography;
using System.Text;
using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;

namespace DevWeave.Infrastructure.Security;

/// <summary>
/// Stateless HMAC-SHA256-signed opaque bearer tokens (SRS TEN-007). A token is a URL-safe two-part value
/// <c>base64url(payload).base64url(signature)</c> where the payload is a versioned, delimiter-separated
/// tuple of (scheme, tenant, principal, principal type, issued-at, expires-at) and the signature is
/// <c>HMACSHA256(signingKey, payload)</c>. Validation is constant-time and never touches the database, so
/// every authenticated request re-verifies identity without a per-call credential lookup.
/// </summary>
public sealed class HmacAccessTokenService : IAccessTokenService
{
    private const string Scheme = "dv1";
    private static readonly TimeSpan MaxLifetime = TimeSpan.FromHours(12);
    private readonly byte[] _key;

    public HmacAccessTokenService(string signingKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signingKey);
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(signingKey));
    }

    public string Issue(TenantId tenantId, PrincipalId principalId, PrincipalType principalType, TimeSpan lifetime)
    {
        if (lifetime <= TimeSpan.Zero || lifetime > MaxLifetime)
            throw new ArgumentOutOfRangeException(nameof(lifetime), "Lifetime must be positive and no more than 12 hours.");

        var issuedAt = DateTimeOffset.UtcNow;
        var expiresAt = issuedAt.Add(lifetime);
        var payload = string.Join('.',
            Scheme,
            tenantId.Value.ToString("N"),
            principalId.Value.ToString("N"),
            ((int)principalType).ToString(),
            issuedAt.ToUnixTimeSeconds().ToString(),
            expiresAt.ToUnixTimeSeconds().ToString());

        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var signature = HMACSHA256.HashData(_key, payloadBytes);

        return $"{Base64UrlEncode(payloadBytes)}.{Base64UrlEncode(signature)}";
    }

    public AuthenticatedSubject? Validate(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var parts = token.Split('.');
        if (parts.Length != 2)
            return null;

        byte[] payloadBytes;
        byte[] signature;
        try
        {
            payloadBytes = Base64UrlDecode(parts[0]);
            signature = Base64UrlDecode(parts[1]);
        }
        catch (FormatException)
        {
            return null;
        }

        var expected = HMACSHA256.HashData(_key, payloadBytes);
        if (!CryptographicOperations.FixedTimeEquals(signature, expected))
            return null;

        var fields = Encoding.UTF8.GetString(payloadBytes).Split('.');
        if (fields.Length != 6 || fields[0] != Scheme)
            return null;

        if (!Guid.TryParseExact(fields[1], "N", out var tenantValue))
            return null;
        if (!Guid.TryParseExact(fields[2], "N", out var principalValue))
            return null;
        if (!int.TryParse(fields[3], out var principalTypeValue) || !Enum.IsDefined(typeof(PrincipalType), principalTypeValue))
            return null;
        if (!long.TryParse(fields[4], out var issuedUnix) || !long.TryParse(fields[5], out var expiresUnix))
            return null;

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (issuedUnix > now || now >= expiresUnix)
            return null;

        return new AuthenticatedSubject(
            new TenantId(tenantValue),
            new PrincipalId(principalValue),
            (PrincipalType)principalTypeValue);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2:
                padded += "==";
                break;
            case 3:
                padded += "=";
                break;
        }
        return Convert.FromBase64String(padded);
    }
}