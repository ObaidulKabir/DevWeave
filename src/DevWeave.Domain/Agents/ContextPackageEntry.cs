using DevWeave.Domain.Knowledge;

namespace DevWeave.Domain.Agents;

/// <summary>
/// A revision-pinned reference included in a context package (SRS AGT-004, AGT-005). The representation
/// kind is snapshotted at assembly time so the package remains auditable even if the item later moves to
/// another representation, and so source-code-bearing content can be excluded from the package.
/// </summary>
public sealed record ContextPackageEntry(
    KnowledgeItemId ItemId,
    KnowledgeRevisionId RevisionId,
    int Position,
    RepresentationKind Representation);