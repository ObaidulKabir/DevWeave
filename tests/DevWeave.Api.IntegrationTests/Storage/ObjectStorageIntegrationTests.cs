using Amazon.S3;
using Amazon.S3.Model;
using DevWeave.Application.Abstractions;
using DevWeave.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace DevWeave.Api.IntegrationTests.Storage;

public sealed class ObjectStorageIntegrationTests
{
    [Fact]
    public async Task In_memory_storage_round_trips_bytes()
    {
        var storage = new InMemoryObjectStorage();
        var payload = "archive-bytes"u8.ToArray();

        await storage.PutAsync("archives/sample.json", payload, "application/json");
        await using var stream = await storage.GetAsync("archives/sample.json");
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);

        Assert.Equal(payload, buffer.ToArray());
    }

    [Fact]
    public async Task S3_storage_round_trips_when_a_test_endpoint_is_configured()
    {
        var endpoint = Environment.GetEnvironmentVariable("DEVWEAVE_TEST_S3_ENDPOINT");
        var accessKey = Environment.GetEnvironmentVariable("DEVWEAVE_TEST_S3_ACCESS_KEY");
        var secretKey = Environment.GetEnvironmentVariable("DEVWEAVE_TEST_S3_SECRET_KEY");
        var bucket = Environment.GetEnvironmentVariable("DEVWEAVE_TEST_S3_BUCKET");
        if (string.IsNullOrWhiteSpace(endpoint) ||
            string.IsNullOrWhiteSpace(accessKey) ||
            string.IsNullOrWhiteSpace(secretKey) ||
            string.IsNullOrWhiteSpace(bucket))
        {
            return;
        }

        var client = new AmazonS3Client(accessKey, secretKey, new AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = true,
            UseHttp = endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase),
            AuthenticationRegion = "us-east-1",
        });
        var key = $"devweave-tests/{Guid.NewGuid():N}.txt";
        var payload = "minio-round-trip"u8.ToArray();
        try
        {
            await client.PutBucketAsync(new PutBucketRequest { BucketName = bucket });
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
        {
        }

        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = new MemoryStream(payload),
            ContentType = "text/plain",
        });

        using var response = await client.GetObjectAsync(bucket, key);
        using var buffer = new MemoryStream();
        await response.ResponseStream.CopyToAsync(buffer);
        await client.DeleteObjectAsync(bucket, key);

        Assert.Equal(payload, buffer.ToArray());
    }

    [Fact]
    public async Task Outbox_claim_query_runs_against_postgres()
    {
        using var factory = new IdentityTestFactory();
        factory.PrepareDatabase();
        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();

        var claimed = await store.ClaimDueAsync(DateTimeOffset.UtcNow, 5);
        await store.SaveAsync();

        Assert.Empty(claimed);
    }
}
