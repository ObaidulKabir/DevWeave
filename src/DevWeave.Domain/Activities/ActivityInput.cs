using DevWeave.Domain.Knowledge;

namespace DevWeave.Domain.Activities;

/// <summary>
/// An immutable, revision-pinned input to an <see cref="Activity"/> (SRS PRO-003). The exact
/// <see cref="KnowledgeRevision"/> consumed is retained, so later revisions never silently rewrite a
/// recorded input. <see cref="Position"/> orders inputs deterministically.
/// </summary>
public sealed record ActivityInput(KnowledgeItemId ItemId, KnowledgeRevisionId RevisionId, int Position);