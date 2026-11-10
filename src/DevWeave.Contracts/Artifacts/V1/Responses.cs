using DevWeave.Domain.Artifacts;
using DevWeave.Domain.Knowledge;

namespace DevWeave.Contracts.Artifacts.V1;

/// <summary>The canonical metadata for one immutable artifact version (SRS ART-001..ART-004, ART-006).</summary>
public sealed record ArtifactResponse(
    Guid Id,
    Guid TenantId,
    Guid ProjectId,
    string Key,
    int Version,
    string MediaType,
    long SizeInBytes,
    string Checksum,
    string ChecksumAlgorithm,
    string ObjectKey,
    Classification Classification,
    Guid Creator,
    DateTimeOffset CreatedAt,
    ArtifactStatus Status);

/// <summary>The result of a checksum verification: ingest-time versus current bytes (SRS ART-006).</summary>
public sealed record ArtifactVerificationResponse(
    Guid ArtifactId,
    string ExpectedChecksum,
    string ActualChecksum,
    string ChecksumAlgorithm,
    bool Match);

/// <summary>A short-lived, access-mediated download grant (SRS ART-004); never a permanent public URL.</summary>
public sealed record ArtifactDownloadResponse(string DownloadUrl, DateTimeOffset ExpiresAt);

/// <summary>A structured artifact error body with a machine-readable code.</summary>
public sealed record ArtifactError(string Code, string Message);