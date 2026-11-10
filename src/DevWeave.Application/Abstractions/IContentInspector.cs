namespace DevWeave.Application.Abstractions;

/// <summary>
/// Content-inspection port run before artifact persistence (SRS ART-008). The verdict either allows the
/// content, rejects it outright, or quarantines it (persisted but flagged). Secrets and source code are
/// classified here; the real adapter lives in infrastructure so the domain and application stay pure.
/// </summary>
public interface IContentInspector
{
    Task<ContentInspectionResult> InspectAsync(
        string fileName,
        string mediaType,
        byte[] content,
        CancellationToken ct = default);
}