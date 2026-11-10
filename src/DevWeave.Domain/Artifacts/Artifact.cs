using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Artifacts;

/// <summary>
/// An append-only binary artifact version (SRS ART-001..ART-004, ART-006, ART-008). Each row is one
/// immutable version of a logical artifact identified by <see cref="Key"/> within a project: the bytes
/// live in object storage under <see cref="ObjectKey"/> while the metadata is canonical in the database
/// (ART-001). There is no mutator and no update/delete surface — a replacement is a new row whose
/// <see cref="Version"/> is the previous version plus one (ART-003), so prior versions are never erased.
/// </summary>
public sealed class Artifact
{
    public ArtifactId Id { get; }
    public TenantId TenantId { get; }
    public ProjectId ProjectId { get; }
    public string Key { get; }
    public int Version { get; }
    public string MediaType { get; }
    public long SizeInBytes { get; }
    public string Checksum { get; }
    public string ChecksumAlgorithm { get; }
    public string ObjectKey { get; }
    public Classification Classification { get; }
    public PrincipalId Creator { get; }
    public DateTimeOffset CreatedAt { get; }
    public ArtifactStatus Status { get; }

    private Artifact(
        ArtifactId id,
        TenantId tenantId,
        ProjectId projectId,
        string key,
        int version,
        string mediaType,
        long sizeInBytes,
        string checksum,
        string checksumAlgorithm,
        string objectKey,
        Classification classification,
        PrincipalId creator,
        DateTimeOffset createdAt,
        ArtifactStatus status)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        Key = key;
        Version = version;
        MediaType = mediaType;
        SizeInBytes = sizeInBytes;
        Checksum = checksum;
        ChecksumAlgorithm = checksumAlgorithm;
        ObjectKey = objectKey;
        Classification = classification;
        Creator = creator;
        CreatedAt = createdAt;
        Status = status;
    }

    public static Artifact Create(
        TenantId tenantId,
        ProjectId projectId,
        string key,
        int version,
        string mediaType,
        long sizeInBytes,
        string checksum,
        string checksumAlgorithm,
        string objectKey,
        Classification classification,
        PrincipalId creator,
        ArtifactStatus status)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Artifact key is required.", nameof(key));

        if (version < 1)
            throw new ArgumentOutOfRangeException(nameof(version), "Artifact version must be at least 1.");

        if (string.IsNullOrWhiteSpace(mediaType))
            throw new ArgumentException("Artifact media type is required.", nameof(mediaType));

        if (sizeInBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(sizeInBytes), "Artifact size cannot be negative.");

        if (string.IsNullOrWhiteSpace(checksum))
            throw new ArgumentException("Artifact checksum is required.", nameof(checksum));

        if (string.IsNullOrWhiteSpace(checksumAlgorithm))
            throw new ArgumentException("Checksum algorithm is required.", nameof(checksumAlgorithm));

        if (string.IsNullOrWhiteSpace(objectKey))
            throw new ArgumentException("Object storage key is required.", nameof(objectKey));

        return new Artifact(
            ArtifactId.New(),
            tenantId,
            projectId,
            key,
            version,
            mediaType,
            sizeInBytes,
            checksum,
            checksumAlgorithm,
            objectKey,
            classification,
            creator,
            DateTimeOffset.UtcNow,
            status);
    }
}