namespace DevWeave.Application.Abstractions;

/// <summary>
/// Object-storage port for artifacts (SRS ART-001..ART-006). Byte-level persistence is isolated behind
/// this port so the canonical database never holds the payload itself, only the object key + checksum.
/// The real adapter targets S3/MinIO; tests substitute an in-memory implementation.
/// </summary>
public interface IObjectStorage
{
    Task PutAsync(string key, byte[] content, string contentType, CancellationToken ct = default);

    Task<Stream> GetAsync(string key, CancellationToken ct = default);

    Task<string> CreatePresignedGetUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default);

    Task DeleteAsync(string key, CancellationToken ct = default);
}