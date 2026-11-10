using System.Globalization;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using DevWeave.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace DevWeave.Infrastructure.Storage;

/// <summary>
/// S3/MinIO adapter for the <see cref="IQuarantineStore"/> port (SRS ART-008). Quarantined bytes are
/// written to a bucket that is distinct from the canonical artifact bucket, so a quarantined object can
/// never be reached through the ordinary artifact object key or a standard presigned download URL. The
/// bounded lifetime is stamped into the object key itself
/// (<c>quarantine/{unix-expiry}/{artifact-key}/{id}</c>) so the cleanup sweep can enumerate and delete
/// expired objects without any canonical-database bookkeeping — quarantine therefore never becomes
/// permanent source retention. Configuration is read from the <c>ObjectStorage:QuarantineBucket</c> key
/// with a safe default; the endpoint/credentials are shared with the canonical adapter.
/// </summary>
public sealed class S3QuarantineStore : IQuarantineStore
{
    private const string Prefix = "quarantine/";

    private readonly string _bucket;
    private readonly AmazonS3Client _client;

    public S3QuarantineStore(IConfiguration configuration)
    {
        var section = configuration.GetSection("ObjectStorage");
        var endpoint = section["Endpoint"];
        _bucket = string.IsNullOrWhiteSpace(section["QuarantineBucket"])
            ? "devweave-quarantine"
            : section["QuarantineBucket"]!;

        if (string.IsNullOrWhiteSpace(endpoint))
            throw new InvalidOperationException("ObjectStorage:Endpoint is not configured.");

        var config = new AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = true,
            UseHttp = endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase),
            AuthenticationRegion = "us-east-1",
        };

        var accessKey = section["AccessKey"];
        var secretKey = section["SecretKey"];

        _client = string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey)
            ? new AmazonS3Client(config)
            : new AmazonS3Client(new BasicAWSCredentials(accessKey, secretKey), config);
    }

    public async Task<string> PutAsync(
        string artifactKey,
        byte[] content,
        string contentType,
        DateTimeOffset expiresAt,
        CancellationToken ct = default)
    {
        var safeArtifactKey = string.Concat(artifactKey.Select(c =>
            char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? char.ToLowerInvariant(c) : '-'));
        var objectKey = $"{Prefix}{expiresAt.ToUnixTimeSeconds()}/{safeArtifactKey}/{Guid.NewGuid():N}";

        using var stream = new MemoryStream(content, writable: false);
        var request = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = objectKey,
            InputStream = stream,
            ContentType = contentType,
            AutoCloseStream = false,
        };

        await _client.PutObjectAsync(request, ct);

        return objectKey;
    }

    public async Task<IReadOnlyList<string>> ListExpiredAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        var expired = new List<string>();
        var threshold = now.ToUnixTimeSeconds();
        string? continuationToken = null;

        do
        {
            var response = await _client.ListObjectsV2Async(
                new ListObjectsV2Request
                {
                    BucketName = _bucket,
                    Prefix = Prefix,
                    ContinuationToken = continuationToken,
                },
                ct);

            foreach (var item in response.S3Objects)
            {
                if (TryParseExpiry(item.Key, out var expiresAt) && expiresAt <= threshold)
                    expired.Add(item.Key);
            }

            continuationToken = response.IsTruncated == true ? response.NextContinuationToken : null;
        }
        while (continuationToken is not null);

        return expired;
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        await _client.DeleteObjectAsync(_bucket, key, ct);
    }

    private static bool TryParseExpiry(string key, out long expiresAtUnixSeconds)
    {
        expiresAtUnixSeconds = 0;
        if (!key.StartsWith(Prefix, StringComparison.Ordinal))
            return false;

        var remainder = key[Prefix.Length..];
        var separator = remainder.IndexOf('/');
        var segment = separator < 0 ? remainder : remainder[..separator];

        return long.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out expiresAtUnixSeconds);
    }
}
