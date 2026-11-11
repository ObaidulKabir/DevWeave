using DevWeave.Domain.Identity;

namespace DevWeave.Domain.Common;

/// <summary>
/// A tenant-scoped message enqueued for asynchronous delivery (SRS coordination, INT-004, DATA-07).
/// Used by the transactional outbox to ensure at-least-once delivery of events.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public TenantId TenantId { get; private set; } = default!;
    public string EventKey { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public string SchemaVersion { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = string.Empty;
    public string CorrelationId { get; private set; } = string.Empty;
    public PrincipalId ActorId { get; private set; } = default!;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }

    private OutboxMessage() { }

    public static OutboxMessage Create(
        TenantId tenantId,
        PrincipalId actorId,
        string eventType,
        string schemaVersion,
        string payloadJson,
        string correlationId)
    {
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ActorId = actorId,
            EventKey = $"{tenantId}:{actorId}:{Guid.NewGuid():N}",
            EventType = eventType,
            SchemaVersion = schemaVersion,
            PayloadJson = payloadJson,
            CorrelationId = correlationId,
            OccurredAt = DateTimeOffset.UtcNow
        };
    }

    public void MarkProcessed()
    {
        ProcessedAt = DateTimeOffset.UtcNow;
    }
}
