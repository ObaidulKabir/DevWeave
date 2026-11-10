namespace DevWeave.Application.Abstractions;

/// <summary>
/// Raised when content is quarantined (persisted with a <see cref="DevWeave.Domain.Artifacts.ArtifactStatus.Quarantined"/>
/// disposition) rather than rejected outright (SRS ART-008). The artifact is durable for review but not
/// served; the API maps this to HTTP 422 with the quarantine note.
/// </summary>
public sealed class QuarantinedContentException : Exception
{
    public QuarantinedContentException(string message, Guid artifactId, string objectKey, IReadOnlyList<string> reasons)
        : base(message)
    {
        ArtifactId = artifactId;
        ObjectKey = objectKey;
        Reasons = reasons;
    }

    public Guid ArtifactId { get; }
    public string ObjectKey { get; }
    public IReadOnlyList<string> Reasons { get; }
}