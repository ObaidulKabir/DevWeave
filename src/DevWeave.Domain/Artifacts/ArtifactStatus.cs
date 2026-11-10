namespace DevWeave.Domain.Artifacts;

/// <summary>
/// The persistence disposition of an artifact version (SRS ART-001, ART-008). <see cref="Stored"/> is
/// the normal durable state; <see cref="Quarantined"/> records that the bytes were persisted for review
/// but flagged by the content inspector and are not served to ordinary consumers.
/// </summary>
public enum ArtifactStatus
{
    Stored = 1,
    Quarantined = 2,
}