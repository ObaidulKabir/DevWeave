namespace DevWeave.Application.Artifacts;

/// <summary>
/// A short-lived, access-mediated download grant for an artifact (SRS ART-004). Only a presigned URL
/// plus its expiry is returned — never a permanent public URL.
/// </summary>
public sealed record ArtifactDownload(string Url, DateTimeOffset ExpiresAt);