using DevWeave.Application.Abstractions.Planned;
using DevWeave.Domain.Common;
using DevWeave.Infrastructure.Persistence;

namespace DevWeave.Infrastructure.Persistence.Outbox;

public sealed class TransactionalOutbox : ITransactionalOutbox
{
    private readonly DevWeaveDbContext _context;

    public TransactionalOutbox(DevWeaveDbContext context)
    {
        _context = context;
    }

    public async Task EnqueueAsync(AuthorizedScope scope, string eventType, string schemaVersion,
        string allowlistedPayloadJson, CancellationToken ct = default)
    {
        var message = OutboxMessage.Create(
            scope.TenantId,
            scope.ActorId,
            eventType,
            schemaVersion,
            allowlistedPayloadJson,
            scope.CorrelationId);

        await _context.Set<OutboxMessage>().AddAsync(message, ct);
    }
}
