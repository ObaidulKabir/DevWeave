using DevWeave.Domain.Common;

namespace DevWeave.Application.Abstractions;

/// <summary>
/// Claims due outbox rows and persists delivery results. A claim holds the rows until
/// <see cref="SaveAsync"/> commits, so a second worker does not take the same batch.
/// </summary>
public interface IOutboxStore
{
    Task<IReadOnlyList<OutboxMessage>> ClaimDueAsync(DateTimeOffset now, int batchSize, CancellationToken ct = default);

    Task SaveAsync(CancellationToken ct = default);
}
