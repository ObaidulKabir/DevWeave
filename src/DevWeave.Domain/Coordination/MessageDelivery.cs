namespace DevWeave.Domain.Coordination;

/// <summary>
/// Recipient progress for one persisted message. Each flag is recorded on its own cursor.
/// </summary>
public sealed record MessageDelivery(
    bool Persisted,
    bool Notified,
    bool Read,
    bool Acknowledged,
    bool Accepted)
{
    public static MessageDelivery ForSequence(long sequence, CoordinationCursor? cursor)
    {
        if (sequence < 1)
            throw new ArgumentOutOfRangeException(nameof(sequence), "A delivery sequence starts at 1.");

        return new MessageDelivery(
            Persisted: true,
            Notified: Reached(cursor?.LastNotifiedSequence, sequence),
            Read: Reached(cursor?.LastReadSequence, sequence),
            Acknowledged: Reached(cursor?.LastAcknowledgedSequence, sequence),
            Accepted: Reached(cursor?.LastAcceptedSequence, sequence));
    }

    private static bool Reached(long? watermark, long sequence) =>
        watermark is long value && value >= sequence;
}
