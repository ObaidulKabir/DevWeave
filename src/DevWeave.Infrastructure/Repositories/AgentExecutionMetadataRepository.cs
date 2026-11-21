using DevWeave.Application.Abstractions;
using DevWeave.Domain.Activities;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevWeave.Infrastructure.Repositories;

internal sealed class AgentExecutionMetadataRepository : IAgentExecutionMetadataRepository
{
    private readonly DevWeaveDbContext _db;

    public AgentExecutionMetadataRepository(DevWeaveDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(AgentExecutionMetadata metadata, CancellationToken ct = default)
    {
        _db.AgentExecutionMetadata.Add(metadata);
        return Task.CompletedTask;
    }

    public Task<AgentExecutionMetadata?> GetByActivityIdAsync(
        TenantId tenantId,
        ActivityId activityId,
        CancellationToken ct = default) =>
        _db.AgentExecutionMetadata
            .SingleOrDefaultAsync(m => m.TenantId == tenantId && m.ActivityId == activityId, ct);

    public async Task<IReadOnlyList<AgentExecutionMetadata>> ListForProjectActivitiesAsync(
        TenantId tenantId,
        IReadOnlyCollection<ActivityId> activityIds,
        CancellationToken ct = default)
    {
        if (activityIds.Count == 0)
            return Array.Empty<AgentExecutionMetadata>();

        var ids = activityIds.Select(id => id.Value).ToArray();
        return await _db.AgentExecutionMetadata
            .Where(m => m.TenantId == tenantId && ids.Contains(m.ActivityId.Value))
            .ToListAsync(ct);
    }
}
