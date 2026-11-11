using DevWeave.Application.Abstractions;
using DevWeave.Domain.Integrations;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevWeave.Infrastructure.Integrations;

public sealed class InboundEventRepository : IInboundEventRepository
{
    private readonly DevWeaveDbContext _context;

    public InboundEventRepository(DevWeaveDbContext context)
    {
        _context = context;
    }

    public async Task<InboundEvent?> GetByProviderEventIdAsync(
        IntegrationId integrationId,
        string providerEventId,
        CancellationToken ct = default)
    {
        return await _context.InboundEvents
            .FirstOrDefaultAsync(e => e.IntegrationId == integrationId && e.ProviderEventId == providerEventId, ct);
    }

    public async Task AddAsync(InboundEvent inboundEvent, CancellationToken ct = default)
    {
        await _context.InboundEvents.AddAsync(inboundEvent, ct);
    }
}
