namespace DevWeave.Application.Abstractions;

/// <summary>The disposition returned by <see cref="IContentInspector"/> for a candidate artifact (ART-008).</summary>
public enum ContentVerdict
{
    Allow = 1,
    Reject = 2,
    Quarantine = 3,
}

/// <summary>The outcome of content inspection: a verdict plus the human-readable reasons behind it.</summary>
public sealed record ContentInspectionResult(ContentVerdict Verdict, IReadOnlyList<string> Reasons)
{
    public static ContentInspectionResult Allow() => new(ContentVerdict.Allow, Array.Empty<string>());

    public static ContentInspectionResult Reject(IReadOnlyList<string> reasons) => new(ContentVerdict.Reject, reasons);

    public static ContentInspectionResult Quarantine(IReadOnlyList<string> reasons) => new(ContentVerdict.Quarantine, reasons);
}