namespace DevWeave.Domain.Knowledge;

/// <summary>
/// Raised when a review decision names a revision that is no longer the item's current revision.
/// A reviewer must decide on the exact content they read; if the item changed in the meantime the
/// decision is refused so approval can never attach to text the reviewer never saw (KNW-012, PRO-005).
/// </summary>
public sealed class StaleReviewException : InvalidOperationException
{
    public StaleReviewException(KnowledgeRevisionId reviewedRevisionId, KnowledgeRevisionId currentRevisionId)
        : base(
            $"Stale review: decision was made on revision '{reviewedRevisionId}' but the item is now at revision '{currentRevisionId}'. Re-read the current revision and decide again.")
    {
        ReviewedRevisionId = reviewedRevisionId;
        CurrentRevisionId = currentRevisionId;
    }

    public KnowledgeRevisionId ReviewedRevisionId { get; }

    public KnowledgeRevisionId CurrentRevisionId { get; }
}
