namespace DevWeave.Application.Abstractions;

/// <summary>
/// Raised when a content inspector rejects an artifact before persistence (SRS ART-008). The API maps
/// this to HTTP 422 with the reasons surfaced in the error body.
/// </summary>
public sealed class ProhibitedContentException : Exception
{
    public ProhibitedContentException(string message, IReadOnlyList<string> reasons)
        : base(message)
    {
        Reasons = reasons;
    }

    public IReadOnlyList<string> Reasons { get; }
}