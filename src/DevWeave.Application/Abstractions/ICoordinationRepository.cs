using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Abstractions;

public interface ICoordinationRepository
{
    // Channels
    Task AddChannelAsync(CoordinationChannel channel, CancellationToken ct = default);
    Task<CoordinationChannel?> GetChannelByIdAsync(TenantId tenantId, ChannelId id, CancellationToken ct = default);
    Task<List<CoordinationChannel>> ListChannelsByProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default);

    // Messages
    Task AddMessageAsync(CoordinationMessage message, CancellationToken ct = default);
    Task<List<CoordinationMessage>> ListMessagesAsync(TenantId tenantId, ChannelId channelId, long sinceSequence, int limit, CancellationToken ct = default);
    Task<CoordinationMessage?> GetMessageByIdAsync(TenantId tenantId, MessageId id, CancellationToken ct = default);
    Task<CoordinationMessage?> GetMessageByIdempotencyKeyAsync(TenantId tenantId, string idempotencyKey, CancellationToken ct = default);

    // Cursors
    Task UpsertCursorAsync(CoordinationCursor cursor, CancellationToken ct = default);
    Task<CoordinationCursor?> GetCursorAsync(TenantId tenantId, ChannelId channelId, PrincipalId principalId, CancellationToken ct = default);

    // Work Packages
    Task AddWorkPackageAsync(WorkPackage workPackage, CancellationToken ct = default);
    Task<WorkPackage?> GetWorkPackageByIdAsync(TenantId tenantId, WorkPackageId id, CancellationToken ct = default);
    Task<WorkPackage?> GetWorkPackageByKeyAsync(TenantId tenantId, CampaignId campaignId, string key, CancellationToken ct = default);

    // Leases
    Task AddLeaseAsync(TaskLease lease, CancellationToken ct = default);
    Task<TaskLease?> GetLeaseByIdAsync(TenantId tenantId, LeaseId id, CancellationToken ct = default);
    Task<TaskLease?> GetActiveLeaseAsync(TenantId tenantId, WorkPackageId workPackageId, string resourceScope, CancellationToken ct = default);
    Task<List<TaskLease>> ListActiveLeasesByRuntimeAsync(TenantId tenantId, RuntimeId runtimeId, CancellationToken ct = default);

    // Runtimes
    Task AddRuntimeAsync(AgentRuntimeInstance runtime, CancellationToken ct = default);
    Task<AgentRuntimeInstance?> GetRuntimeByIdAsync(TenantId tenantId, RuntimeId id, CancellationToken ct = default);
    Task<AgentRuntimeInstance?> GetRuntimeBySessionKeyAsync(string sessionKey, CancellationToken ct = default);

    // Baselines
    Task AddBaselineAsync(PlanBaseline baseline, CancellationToken ct = default);
    Task<PlanBaseline?> GetBaselineByIdAsync(TenantId tenantId, BaselineId id, CancellationToken ct = default);
    Task<List<PlanBaseline>> ListBaselinesByProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default);

    // Codebase Targets
    Task AddCodebaseTargetAsync(CodebaseTarget target, CancellationToken ct = default);
    Task<CodebaseTarget?> GetCodebaseTargetByIdAsync(TenantId tenantId, CodebaseTargetId id, CancellationToken ct = default);

    // Change Sets
    Task AddChangeSetAsync(ChangeSet changeSet, CancellationToken ct = default);
    Task<ChangeSet?> GetChangeSetByIdAsync(TenantId tenantId, ChangeSetId id, CancellationToken ct = default);
    Task<ChangeSet?> GetChangeSetByIdempotencyKeyAsync(TenantId tenantId, string idempotencyKey, CancellationToken ct = default);

    // Offers
    Task AddOfferAsync(ParticipationOffer offer, CancellationToken ct = default);
    Task<ParticipationOffer?> GetOfferByIdAsync(TenantId tenantId, ParticipationOfferId id, CancellationToken ct = default);

    // Integration Queue
    Task AddQueueItemAsync(IntegrationQueueItem item, CancellationToken ct = default);
    Task<IntegrationQueueItem?> GetQueueItemByIdAsync(TenantId tenantId, IntegrationQueueItemId id, CancellationToken ct = default);
    Task<IntegrationQueueItem?> GetQueueItemByIdempotencyKeyAsync(TenantId tenantId, string idempotencyKey, CancellationToken ct = default);

    // Kill Switches
    Task AddKillSwitchAsync(CoordinationKillSwitch killSwitch, CancellationToken ct = default);
    Task<bool> IsTargetKilledAsync(TenantId tenantId, KillSwitchTargetType type, Guid targetId, CancellationToken ct = default);

    // Campaigns
    Task<ExecutionCampaign?> GetCampaignByIdAsync(TenantId tenantId, CampaignId id, CancellationToken ct = default);
    Task<List<ExecutionCampaign>> ListCampaignsByProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default);
    Task AddCampaignAsync(ExecutionCampaign campaign, CancellationToken ct = default);

    // Work packages by campaign
    Task<List<WorkPackage>> ListWorkPackagesByCampaignAsync(TenantId tenantId, CampaignId campaignId, CancellationToken ct = default);

    // Runtimes by project
    Task<List<AgentRuntimeInstance>> ListRuntimesByProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default);
}
