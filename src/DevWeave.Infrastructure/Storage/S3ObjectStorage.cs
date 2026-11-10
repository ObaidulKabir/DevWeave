using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using DevWeave.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace DevWeave.Infrastructure.Storage;

/// <summary>
/// S3/MinIO adapter for the <see cref="IObjectStorage"/> port (SRS ART-001, ART-004). Byte payloads live
/// in the bucket under the artifact's object key; canonical metadata stays in the database. Downloads are
/// granted only through short-lived presigned URLs, never permanent public URLs. Configuration is read
/// from the <c>ObjectStorage</c> section (Endpoint, AccessKey, SecretKey, Bucket) so this adapter is a
/// thin boundary over the vendor SDK and is substituted by an in-memory fake in tests.
/// </summary>
public sealed class S3ObjectStorage : IObjectStorage
{
    private readonly string _bucket;
    private readonly AmazonS3Client _client;

    public S3ObjectStorage(IConfiguration configuration)
    {
        var section = configuration.GetSection("ObjectStorage");
        var endpoint = section["Endpoint"];
        _bucket = string.IsNullOrWhiteSpace(section["Bucket"]) ? "devweave-artifacts" : section["Bucket"]!;

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

    public async Task PutAsync(string key, byte[] content, string contentType, CancellationToken ct = default)
    {
        using var stream = new MemoryStream(content, writable: false);
        var request = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = stream,
            ContentType = contentType,
            AutoCloseStream = false,
        };

        await _client.PutObjectAsync(request, ct);
    }

    public async Task<Stream> GetAsync(string key, CancellationToken ct = default)
    {
        var response = await _client.GetObjectAsync(_bucket, key, ct);
        return response.ResponseStream;
    }

    public Task<string> CreatePresignedGetUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Expires = DateTime.UtcNow.Add(expiry),
            Verb = HttpVerb.GET,
        };

        return Task.FromResult(_client.GetPreSignedURL(request));
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        await _client.DeleteObjectAsync(_bucket, key, ct);
    }
}