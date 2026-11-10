namespace DevWeave.Application.Artifacts;

/// <summary>
/// The pure result of verifying an artifact's integrity (SRS ART-006): the checksum recorded at ingest
/// versus the checksum recomputed from the current object-storage bytes, plus whether they match.
/// </summary>
public sealed record ArtifactVerificationResult(
    Guid ArtifactId,
    string ExpectedChecksum,
    string ActualChecksum,
    string ChecksumAlgorithm,
    bool Match);