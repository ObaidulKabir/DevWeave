using System.Text.Json;
using DevWeave.Application.Abstractions;
using DevWeave.Domain.Audit;
using DevWeave.Domain.Common;

namespace DevWeave.Application.Delivery;

/// <summary>
/// Records a durable delivery for each outbox message. The payload stays on the outbox row.
/// </summary>
public sealed class OutboxDeliveryConsumer
{
    public const string Action = "OutboxDelivered";
    public const string ResourceType = "OutboxMessage";

    private readonly IAuditRepository _audit;

    public OutboxDeliveryConsumer(IAuditRepository audit)
    {
        _audit = audit;
    }

    public async Task ConsumeAsync(OutboxMessage message, CancellationToken ct = default)
    {
        var resourceId = message.Id.ToString("D");
        var existing = await _audit.ListForResourceAsync(message.TenantId, ResourceType, resourceId, ct);
        if (existing.Exists(record => record.Action == Action))
            return;

        var record = AuditRecord.Record(
            message.TenantId,
            message.ActorId,
            Action,
            ResourceType,
            resourceId,
            JsonSerializer.Serialize(new { eventType = message.EventType, schemaVersion = message.SchemaVersion }),
            message.CorrelationId);
        await _audit.AddAsync(record, ct);
    }
}
