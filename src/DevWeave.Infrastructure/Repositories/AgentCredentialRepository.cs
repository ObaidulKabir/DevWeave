using DevWeave.Application.Abstractions;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevWeave.Infrastructure.Repositories;

internal sealed class AgentCredentialRepository : IAgentCredentialRepository
{
    private readonly DevWeaveDbContext _db;

    public AgentCredentialRepository(DevWeaveDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(AgentCredential credential, CancellationToken ct = default)
    {
        _db.AgentCredentials.Add(credential);
        return Task.CompletedTask;
    }

    // Deny-by-default: a cross-tenant credential is indistinguishable from a missing one.
    public Task<AgentCredential?> GetByIdAsync(TenantId tenantId, AgentCredentialId id, CancellationToken ct = default) =>
        _db.AgentCredentials.SingleOrDefaultAsync(c => c.TenantId == tenantId && c.Id == id, ct);

    public async Task<IReadOnlyList<AgentCredential>> ListForAgentAsync(TenantId tenantId, PrincipalId agentId, CancellationToken ct = default) =>
        await _db.AgentCredentials
            .Where(c => c.TenantId == tenantId && c.AgentId == agentId)
            .ToListAsync(ct);
}