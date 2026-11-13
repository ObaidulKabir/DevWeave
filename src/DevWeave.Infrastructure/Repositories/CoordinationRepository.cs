using DevWeave.Application.Abstractions;
using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevWeave.Infrastructure.Repositories;

public sealed class CoordinationRepository : ICoordinationRepository
{
    private readonly DevWeaveDbContext _context;

    public CoordinationRepository(DevWeaveDbContext context)
    {
        _context = context;
    }

    public async Task AddChannelAsync(CoordinationChannel channel, CancellationToken ct = default)
    {
        await _context.CoordinationChannels.AddAsync(channel, ct);
    }

    public async Task<CoordinationChannel?> GetChannelByIdAsync(TenantId tenantId, ChannelId id, CancellationToken ct = default)
    {
        return await _context.CoordinationChannels
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == id, ct);
    }

    public async Task<List<CoordinationChannel>> ListChannelsByProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default)
    {
        return await _context.CoordinationChannels
            .Where(c => c.TenantId == tenantId && c.ProjectId == projectId)
            .ToListAsync(ct);
    }

    public async Task AddMessageAsync(CoordinationMessage message, CancellationToken ct = default)
    {
        await _context.CoordinationMessages.AddAsync(message, ct);
    }

    public async Task<List<CoordinationMessage>> ListMessagesAsync(TenantId tenantId, ChannelId channelId, long sinceSequence, int limit, CancellationToken ct = default)
    {
        return await _context.CoordinationMessages
            .Where(m => m.TenantId == tenantId && m.ChannelId == channelId && m.Sequence > sinceSequence)
            .OrderBy(m => m.Sequence)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<CoordinationMessage?> GetMessageByIdAsync(TenantId tenantId, MessageId id, CancellationToken ct = default)
    {
        return await _context.CoordinationMessages
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == id, ct);
    }

    public async Task<CoordinationMessage?> GetMessageByIdempotencyKeyAsync(TenantId tenantId, string idempotencyKey, CancellationToken ct = default)
    {
        return await _context.CoordinationMessages
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.IdempotencyKey == idempotencyKey, ct);
    }

    public async Task UpsertCursorAsync(CoordinationCursor cursor, CancellationToken ct = default)
    {
        var existing = await _context.CoordinationCursors
            .FirstOrDefaultAsync(c => c.ChannelId == cursor.ChannelId && c.PrincipalId == cursor.PrincipalId, ct);

        if (existing == null)
        {
            await _context.CoordinationCursors.AddAsync(cursor, ct);
        }
        else
        {
            existing.UpdateAcknowledged(cursor.LastAcknowledgedSequence);
            existing.UpdateRead(cursor.LastReadSequence);
        }
    }

    public async Task<CoordinationCursor?> GetCursorAsync(TenantId tenantId, ChannelId channelId, PrincipalId principalId, CancellationToken ct = default)
    {
        return await _context.CoordinationCursors
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.ChannelId == channelId && c.PrincipalId == principalId, ct);
    }

    public async Task AddCampaignAsync(ExecutionCampaign campaign, CancellationToken ct = default)
    {
        await _context.ExecutionCampaigns.AddAsync(campaign, ct);
    }

    public async Task<ExecutionCampaign?> GetCampaignByIdAsync(TenantId tenantId, CampaignId id, CancellationToken ct = default)
    {
        return await _context.ExecutionCampaigns
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == id, ct);
    }

    public async Task<List<ExecutionCampaign>> ListCampaignsByProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default)
    {
        return await _context.ExecutionCampaigns
            .Where(c => c.TenantId == tenantId && c.ProjectId == projectId)
            .ToListAsync(ct);
    }

    public async Task AddWorkPackageAsync(WorkPackage workPackage, CancellationToken ct = default)
    {
        await _context.WorkPackages.AddAsync(workPackage, ct);
    }

    public async Task<WorkPackage?> GetWorkPackageByIdAsync(TenantId tenantId, WorkPackageId id, CancellationToken ct = default)
    {
        return await _context.WorkPackages
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.Id == id, ct);
    }

    public async Task<WorkPackage?> GetWorkPackageByKeyAsync(TenantId tenantId, CampaignId campaignId, string key, CancellationToken ct = default)
    {
        return await _context.WorkPackages
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.CampaignId == campaignId && w.Key == key, ct);
    }

    public async Task<List<WorkPackage>> ListWorkPackagesByCampaignAsync(TenantId tenantId, CampaignId campaignId, CancellationToken ct = default)
    {
        return await _context.WorkPackages
            .Where(w => w.TenantId == tenantId && w.CampaignId == campaignId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task AddLeaseAsync(TaskLease lease, CancellationToken ct = default)
    {
        await _context.TaskLeases.AddAsync(lease, ct);
    }

    public async Task<TaskLease?> GetLeaseByIdAsync(TenantId tenantId, LeaseId id, CancellationToken ct = default)
    {
        return await _context.TaskLeases
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.Id == id, ct);
    }

    public async Task<TaskLease?> GetActiveLeaseAsync(TenantId tenantId, WorkPackageId workPackageId, string resourceScope, CancellationToken ct = default)
    {
        return await _context.TaskLeases
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && 
                                     l.WorkPackageId == workPackageId && 
                                     l.ResourceScope == resourceScope && 
                                     l.Status == LeaseStatus.Active && 
                                     l.ExpiresAt > DateTimeOffset.UtcNow, ct);
    }

    public async Task<List<TaskLease>> ListActiveLeasesByRuntimeAsync(TenantId tenantId, RuntimeId runtimeId, CancellationToken ct = default)
    {
        return await _context.TaskLeases
            .Where(l => l.TenantId == tenantId && l.RuntimeId == runtimeId && l.Status == LeaseStatus.Active)
            .ToListAsync(ct);
    }

    public async Task AddRuntimeAsync(AgentRuntimeInstance runtime, CancellationToken ct = default)
    {
        await _context.AgentRuntimeInstances.AddAsync(runtime, ct);
    }

    public async Task<AgentRuntimeInstance?> GetRuntimeByIdAsync(TenantId tenantId, RuntimeId id, CancellationToken ct = default)
    {
        return await _context.AgentRuntimeInstances
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == id, ct);
    }

    public async Task<AgentRuntimeInstance?> GetRuntimeBySessionKeyAsync(string sessionKey, CancellationToken ct = default)
    {
        return await _context.AgentRuntimeInstances
            .FirstOrDefaultAsync(r => r.SessionKey == sessionKey, ct);
    }

    public async Task<List<AgentRuntimeInstance>> ListRuntimesByProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default)
    {
        return await _context.AgentRuntimeInstances
            .Where(r => r.TenantId == tenantId && r.ProjectId == projectId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
    }

    // Baselines
    public async Task AddBaselineAsync(PlanBaseline baseline, CancellationToken ct = default)
    {
        await _context.PlanBaselines.AddAsync(baseline, ct);
    }

    public async Task<PlanBaseline?> GetBaselineByIdAsync(TenantId tenantId, BaselineId id, CancellationToken ct = default)
    {
        return await _context.PlanBaselines
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.Id == id, ct);
    }

    public async Task<List<PlanBaseline>> ListBaselinesByProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default)
    {
        return await _context.PlanBaselines
            .Where(b => b.TenantId == tenantId && b.ProjectId == projectId)
            .OrderByDescending(b => b.ExpectedPlanVersion)
            .ThenByDescending(b => b.CreatedAt)
            .ToListAsync(ct);
    }

    // Codebase Targets
    public async Task AddCodebaseTargetAsync(CodebaseTarget target, CancellationToken ct = default)
    {
        await _context.CodebaseTargets.AddAsync(target, ct);
    }

    public async Task<CodebaseTarget?> GetCodebaseTargetByIdAsync(TenantId tenantId, CodebaseTargetId id, CancellationToken ct = default)
    {
        return await _context.CodebaseTargets
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == id, ct);
    }

    // Change Sets
    public async Task AddChangeSetAsync(ChangeSet changeSet, CancellationToken ct = default)
    {
        await _context.ChangeSets.AddAsync(changeSet, ct);
    }

    public async Task<ChangeSet?> GetChangeSetByIdAsync(TenantId tenantId, ChangeSetId id, CancellationToken ct = default)
    {
        return await _context.ChangeSets
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == id, ct);
    }

    public async Task<ChangeSet?> GetChangeSetByIdempotencyKeyAsync(TenantId tenantId, string idempotencyKey, CancellationToken ct = default)
    {
        return await _context.ChangeSets
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && 
                                     c.ProviderRef == idempotencyKey, ct); // Using ProviderRef as idempotency key for now
    }

    // Offers
    public async Task AddOfferAsync(ParticipationOffer offer, CancellationToken ct = default)
    {
        await _context.ParticipationOffers.AddAsync(offer, ct);
    }

    public async Task<ParticipationOffer?> GetOfferByIdAsync(TenantId tenantId, ParticipationOfferId id, CancellationToken ct = default)
    {
        return await _context.ParticipationOffers
            .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == id, ct);
    }

    // Integration Queue
    public async Task AddQueueItemAsync(IntegrationQueueItem item, CancellationToken ct = default)
    {
        await _context.IntegrationQueueItems.AddAsync(item, ct);
    }

    public async Task<IntegrationQueueItem?> GetQueueItemByIdAsync(TenantId tenantId, IntegrationQueueItemId id, CancellationToken ct = default)
    {
        return await _context.IntegrationQueueItems
            .FirstOrDefaultAsync(i => i.TenantId == tenantId && i.Id == id, ct);
    }

    public async Task<IntegrationQueueItem?> GetQueueItemByIdempotencyKeyAsync(TenantId tenantId, string idempotencyKey, CancellationToken ct = default)
    {
        return await _context.IntegrationQueueItems
            .FirstOrDefaultAsync(i => i.TenantId == tenantId && i.IdempotencyKey == idempotencyKey, ct);
    }

    // Kill Switches
    public async Task AddKillSwitchAsync(CoordinationKillSwitch killSwitch, CancellationToken ct = default)
    {
        await _context.CoordinationKillSwitches.AddAsync(killSwitch, ct);
    }

    public async Task<bool> IsTargetKilledAsync(TenantId tenantId, KillSwitchTargetType type, Guid targetId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await _context.CoordinationKillSwitches
            .AnyAsync(k => k.TenantId == tenantId && 
                          k.TargetType == type && 
                          k.TargetId == targetId && 
                          k.IsActive &&
                          (k.ExpiresAt == null || k.ExpiresAt > now), ct);
    }
}
