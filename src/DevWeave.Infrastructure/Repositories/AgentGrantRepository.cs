using DevWeave.Application.Abstractions;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevWeave.Infrastructure.Repositories;

internal sealed class AgentGrantRepository : IAgentGrantRepository
{
    private readonly DevWeaveDbContext _db;

    public AgentGrantRepository(DevWeaveDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(AgentGrant grant, CancellationToken ct = default)
    {
        _db.AgentGrants.Add(grant);
        return Task.CompletedTask;
    }

    // Deny-by-default: a cross-tenant grant is indistinguishable from a missing one.
    public Task<AgentGrant?> GetByIdAsync(TenantId tenantId, AgentGrantId id, CancellationToken ct = default) =>
        _db.AgentGrants.SingleOrDefaultAsync(g => g.TenantId == tenantId && g.Id == id, ct);

    public async Task<IReadOnlyList<AgentGrant>> ListForAgentAsync(TenantId tenantId, PrincipalId agentId, CancellationToken ct = default) =>
        await _db.AgentGrants
            .Where(g => g.TenantId == tenantId && g.AgentId == agentId)
            .ToListAsync(ct);
}