using System.Collections.Concurrent;
using DevWeave.Application.Abstractions;

namespace DevWeave.Api.IntegrationTests;

/// <summary>
/// In-memory <see cref="IQuarantineStore"/> stand-in for integration tests. The real S3/MinIO adapter is
/// swapped out through the test factory so quarantined artifact ingestion can be exercised without an
/// external object store, while still proving flagged bytes never reach canonical storage (SRS ART-008).
/// </summary>
internal sealed class InMemoryQuarantineStore : IQuarantineStore
{
    private readonly ConcurrentDictionary<string, (byte[] Content, DateTimeOffset ExpiresAt)> _objects =
        new(StringComparer.Ordinal);

    public Task<string> PutAsync(
        string artifactKey,
        byte[] content,
        string contentType,
        DateTimeOffset expiresAt,
        CancellationToken ct = default)
    {
        var key = $"quarantine/{expiresAt.ToUnixTimeSeconds()}/{Guid.NewGuid():N}";
        _objects[key] = (content.ToArray(), expiresAt);
        return Task.FromResult(key);
    }

    public Task<IReadOnlyList<string>> ListExpiredAsync(DateTimeOffset now, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<string>>(
            _objects.Where(o => o.Value.ExpiresAt <= now).Select(o => o.Key).ToList());

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        _objects.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
