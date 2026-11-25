using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Archives;

/// <summary>
/// A durable export or import job. The object-storage package is the bytes; this row is the
/// idempotency and identity-map record.
/// </summary>
public sealed class ArchiveJob
{
    public Guid Id { get; private set; }
    public TenantId TenantId { get; private set; } = default!;
    public ProjectId ProjectId { get; private set; } = default!;
    public string Kind { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string ObjectKey { get; private set; } = string.Empty;
    public string Checksum { get; private set; } = string.Empty;
    public string FormatVersion { get; private set; } = string.Empty;
    public string? IdentityMapJson { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private ArchiveJob() { }

    public static ArchiveJob Create(
        TenantId tenantId,
        ProjectId projectId,
        string kind,
        string idempotencyKey,
        string objectKey,
        string checksum,
        string formatVersion,
        string? identityMapJson)
    {
        if (string.IsNullOrWhiteSpace(kind))
            throw new ArgumentException("Archive job kind is required.", nameof(kind));
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));
        if (string.IsNullOrWhiteSpace(objectKey))
            throw new ArgumentException("Object key is required.", nameof(objectKey));
        if (string.IsNullOrWhiteSpace(checksum))
            throw new ArgumentException("Checksum is required.", nameof(checksum));
        if (string.IsNullOrWhiteSpace(formatVersion))
            throw new ArgumentException("Format version is required.", nameof(formatVersion));

        return new ArchiveJob
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = projectId,
            Kind = kind.Trim(),
            IdempotencyKey = idempotencyKey.Trim(),
            ObjectKey = objectKey.Trim(),
            Checksum = checksum.Trim(),
            FormatVersion = formatVersion.Trim(),
            IdentityMapJson = identityMapJson,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }
}
