using DevWeave.Application.Abstractions;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Contracts.Planned.V1;
using DevWeave.Domain.Integrations;

namespace DevWeave.Application.Integrations;

public sealed class InboundEventProcessor : IInboundEventProcessor
{
    private readonly IIntegrationRepository _integrationRepository;
    private readonly IInboundEventRepository _eventRepository;
    private readonly ITransactionalOutbox _outbox;
    private readonly IUnitOfWork _uow;

    public InboundEventProcessor(
        IIntegrationRepository integrationRepository,
        IInboundEventRepository eventRepository,
        ITransactionalOutbox outbox,
        IUnitOfWork uow)
    {
        _integrationRepository = integrationRepository;
        _eventRepository = eventRepository;
        _outbox = outbox;
        _uow = uow;
    }

    public async Task<Decision> AcceptAsync(AuthorizedScope scope, Guid connectionId, IngressEnvelope envelope,
        CancellationToken ct = default)
    {
        var integration = await _integrationRepository.GetByIdAsync(new IntegrationId(connectionId), ct);
        if (integration == null || integration.TenantId != scope.TenantId)
        {
            return new Decision("Rejected", "Integration not found or unauthorized.", "1.0");
        }

        if (integration.Status != IntegrationStatus.Connected)
        {
            return new Decision("Rejected", $"Integration is in status {integration.Status}.", "1.0");
        }

        // provider/event dedupe (SRS INT-004, DATA-07)
        var existingEvent = await _eventRepository.GetByProviderEventIdAsync(
            integration.Id,
            envelope.ProviderEventId,
            ct);

        if (existingEvent != null)
        {
            return new Decision("Conflict", "Event already processed.", "1.0");
        }

        // Record the event
        var inboundEvent = InboundEvent.Record(
            integration.Id,
            envelope.ProviderEventId,
            envelope.SentAt,
            envelope.MetadataJson);

        await _eventRepository.AddAsync(inboundEvent, ct);

        // TODO: Apply metadata allowlist filters here if necessary before outbox enqueue
        // For now, we assume the envelope contains allowed fields or the outbox port handles it.

        // Enqueue to outbox for downstream processing
        await _outbox.EnqueueAsync(
            scope,
            $"IntegrationEvent:{integration.Provider}",
            envelope.SchemaVersion,
            envelope.MetadataJson,
            ct);

        inboundEvent.MarkProcessed();
        await _uow.SaveChangesAsync(ct);

        return new Decision("Accepted", "Event recorded and enqueued for processing.", "1.0");
    }
}
