using System.Collections.Concurrent;
using DevWeave.Application.Abstractions;

namespace DevWeave.Api.IntegrationTests;

/// <summary>
/// In-memory <see cref="IObjectStorage"/> stand-in for integration tests. The real S3/MinIO adapter is
/// swapped out through the test factory so artifact round-trips never require an external object store,
/// while still exercising the full application + endpoint pipeline (SRS ART-001..ART-004, ART-006).
/// </summary>
internal sealed class InMemoryObjectStorage : IObjectStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _objects = new(StringComparer.Ordinal);

    public Task PutAsync(string key, byte[] content, string contentType, CancellationToken ct = default)
    {
        _objects[key] = content.ToArray();
        return Task.CompletedTask;
    }

    public Task<Stream> GetAsync(string key, CancellationToken ct = default)
    {
        var data = _objects[key];
        return Task.FromResult<Stream>(new MemoryStream(data, writable: false));
    }

    public Task<string> CreatePresignedGetUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default) =>
        Task.FromResult($"https://s3.example.test/{key}?X-Amz-Expires={(long)expiry.TotalSeconds}&X-Amz-Signature=test");

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        _objects.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}