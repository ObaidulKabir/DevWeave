using DevWeave.Application.Abstractions;
using DevWeave.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DevWeave.Infrastructure.Persistence.Outbox;

/// <summary>
/// Claims due outbox rows with <c>FOR UPDATE SKIP LOCKED</c> so two workers do not deliver the same message.
/// </summary>
public sealed class EfOutboxStore : IOutboxStore
{
    private readonly DevWeaveDbContext _db;
    private IDbContextTransaction? _transaction;

    public EfOutboxStore(DevWeaveDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<OutboxMessage>> ClaimDueAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken ct = default)
    {
        _transaction ??= await _db.Database.BeginTransactionAsync(ct);
        var ids = await _db.Database.SqlQuery<Guid>(
            $"""
            SELECT "Id" AS "Value"
            FROM outbox_messages
            WHERE "ProcessedAt" IS NULL
              AND "PoisonedAt" IS NULL
              AND "AvailableAt" <= {now}
            ORDER BY "OccurredAt"
            LIMIT {batchSize}
            FOR UPDATE SKIP LOCKED
            """).ToListAsync(ct);

        if (ids.Count == 0)
            return Array.Empty<OutboxMessage>();

        return await _db.OutboxMessages.Where(message => ids.Contains(message.Id)).ToListAsync(ct);
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        await _db.SaveChangesAsync(ct);
        if (_transaction is null)
            return;

        await _transaction.CommitAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
    }
}
