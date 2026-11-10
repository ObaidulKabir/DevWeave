using DevWeave.Domain.Knowledge;

namespace DevWeave.Domain.Activities;

/// <summary>
/// An immutable, revision-pinned output produced by an <see cref="Activity"/> (SRS PRO-004). The
/// producing activity and responsible actor are attached on the aggregate, while here the exact
/// generated revision and its position are retained.
/// </summary>
public sealed record ActivityOutput(KnowledgeItemId ItemId, KnowledgeRevisionId RevisionId, int Position);