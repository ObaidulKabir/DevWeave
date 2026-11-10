using System.Security.Cryptography;
using DevWeave.Application.Abstractions;

namespace DevWeave.Infrastructure.Security;

/// <summary>
/// Password-hardening key derivation for agent secrets (SRS AGT-002). Replaces the plain SHA-256
/// placeholder with PBKDF2-HMAC-SHA256 at OWASP-recommended cost (210,000 iterations) plus a per-secret
/// random salt, so a compromised database cannot be brute-forced with the same ease as a bare digest.
/// The encoded form is <c>pbkdf2-sha256$iterations$salt$derivedKey</c>, and verification is
/// constant-time via <see cref="CryptographicOperations.FixedTimeEquals"/>.
/// </summary>
public sealed class Pbkdf2SecretHasher : ISecretHasher
{
    private const string Prefix = "pbkdf2-sha256";
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public string Hash(string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(secret, salt, Iterations, Algorithm, KeySize);

        return string.Join('$',
            Prefix,
            Iterations.ToString(),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(key));
    }

    public bool Verify(string secret, string hash)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(hash))
        {
            return false;
        }

        var parts = hash.Split('$');
        if (parts.Length != 4 || parts[0] != Prefix)
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var iterations) || iterations <= 0)
        {
            return false;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(secret, salt, iterations, Algorithm, expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}