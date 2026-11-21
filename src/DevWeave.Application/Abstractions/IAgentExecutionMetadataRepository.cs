using DevWeave.Domain.Activities;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;

namespace DevWeave.Application.Abstractions;

public interface IAgentExecutionMetadataRepository
{
    Task AddAsync(AgentExecutionMetadata metadata, CancellationToken ct = default);

    Task<AgentExecutionMetadata?> GetByActivityIdAsync(
        TenantId tenantId,
        ActivityId activityId,
        CancellationToken ct = default);

    Task<IReadOnlyList<AgentExecutionMetadata>> ListForProjectActivitiesAsync(
        TenantId tenantId,
        IReadOnlyCollection<ActivityId> activityIds,
        CancellationToken ct = default);
}
