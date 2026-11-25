using DevWeave.Application.Abstractions;
using DevWeave.Domain.Common;

namespace DevWeave.Application.Delivery;

/// <summary>
/// Delivers a claimed outbox batch. Success marks the row processed. Failure backs off and,
/// after <see cref="MaxAttempts"/>, poisons the row so it is not retried forever.
/// </summary>
public sealed class OutboxDispatcher
{
    public const int MaxAttempts = 5;
    public const int DefaultBatchSize = 25;

    private readonly IOutboxStore _store;

    public OutboxDispatcher(IOutboxStore store)
    {
        _store = store;
    }

    public async Task<int> DispatchAsync(
        Func<OutboxMessage, CancellationToken, Task> consume,
        CancellationToken ct = default,
        int batchSize = DefaultBatchSize)
    {
        ArgumentNullException.ThrowIfNull(consume);
        var batch = await _store.ClaimDueAsync(DateTimeOffset.UtcNow, batchSize, ct);
        var delivered = 0;
        foreach (var message in batch)
        {
            try
            {
                await consume(message, ct);
                message.MarkProcessed();
                delivered++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.RecordFailure(ex.Message, DateTimeOffset.UtcNow, MaxAttempts);
            }
        }

        await _store.SaveAsync(ct);
        return delivered;
    }
}
