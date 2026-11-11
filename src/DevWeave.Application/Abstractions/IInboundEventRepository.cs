using DevWeave.Domain.Integrations;

namespace DevWeave.Application.Abstractions;

public interface IInboundEventRepository
{
    Task<InboundEvent?> GetByProviderEventIdAsync(
        IntegrationId integrationId,
        string providerEventId,
        CancellationToken ct = default);

    Task AddAsync(InboundEvent inboundEvent, CancellationToken ct = default);
}
