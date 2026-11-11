using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Integrations;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevWeave.Infrastructure.Integrations;

public sealed class IntegrationRepository : IIntegrationRepository
{
    private readonly DevWeaveDbContext _context;

    public IntegrationRepository(DevWeaveDbContext context)
    {
        _context = context;
    }

    public async Task<Integration?> GetByIdAsync(IntegrationId id, CancellationToken ct = default)
    {
        return await _context.Integrations.FindAsync(new object[] { id }, ct);
    }

    public async Task<IReadOnlyList<Integration>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default)
    {
        return await _context.Integrations
            .Where(i => i.TenantId == tenantId)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Integration integration, CancellationToken ct = default)
    {
        await _context.Integrations.AddAsync(integration, ct);
    }
}
