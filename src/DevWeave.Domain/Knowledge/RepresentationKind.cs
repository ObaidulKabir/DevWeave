namespace DevWeave.Domain.Knowledge;

/// <summary>
/// The kind of content representation a knowledge item carries (SRS KNW-005). The identity of an
/// item is independent of this representation, so the same item can move between representations
/// across revisions without changing its stable id.
/// </summary>
public enum RepresentationKind
{
    Text = 1,
    StructuredData = 2,
    Diagram = 3,
    Chart = 4,
    Document = 5,
    Media = 6,
    ExternalReference = 7,
    SourceCode = 8,
}