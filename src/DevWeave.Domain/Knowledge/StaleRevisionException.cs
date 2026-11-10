namespace DevWeave.Domain.Knowledge;

/// <summary>
/// Raised when a revision is written against a stale revision number (SRS REV-004). Optimistic
/// concurrency treats this as a conflict: the caller's expected current revision no longer matches
/// the item, so the write must be retried against the latest state rather than silently applied.
/// </summary>
public sealed class StaleRevisionException : InvalidOperationException
{
    public StaleRevisionException(int expectedRevisionNumber, int currentRevisionNumber)
        : base(
            $"Stale revision: expected current revision '{expectedRevisionNumber}' but the item is at revision '{currentRevisionNumber}'.")
    {
        ExpectedRevisionNumber = expectedRevisionNumber;
        CurrentRevisionNumber = currentRevisionNumber;
    }

    public int ExpectedRevisionNumber { get; }

    public int CurrentRevisionNumber { get; }
}