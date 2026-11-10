using System.Security.Cryptography;

namespace DevWeave.Application.Artifacts;

/// <summary>
/// Pure checksum helpers shared by upload and verify (SRS ART-006). Kept in the application layer so
/// checksum computation and the upload/verify orchestration remain technology-neutral; only the byte
/// hashing primitive from .NET is used.
/// </summary>
internal static class Checksums
{
    public static string ComputeSha256(ReadOnlySpan<byte> content)
    {
        var hash = SHA256.HashData(content);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}