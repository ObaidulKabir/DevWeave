using DevWeave.Contracts.Planned.V1;
using DevWeave.Domain.Coordination;

namespace DevWeave.Application.Abstractions.Planned;

// Coordination ports are proposal-only (MCO-001..048, MCP-001..036). No method authorizes
// itself: implementations must recheck the authenticated scope, baseline, grant and fence.
public interface IPlanBaselines
{
    Task<Guid> LockAsync(AuthorizedScope scope, BaselineDraft draft, CancellationToken ct = default);
    Task<ExportManifest> DescribeAsync(AuthorizedScope scope, Guid baselineId,
        CancellationToken ct = default);
}

public interface ICodebaseRegistry
{
    Task<Guid> RegisterAsync(AuthorizedScope scope, string provider, string repositoryId,
        string targetBranch, string baseCommitSha, CancellationToken ct = default);
    Task<Guid> RecordChangeSetAsync(AuthorizedScope scope, ChangeSetDraft draft,
        CancellationToken ct = default);
}

public interface ICampaignCoordinator
{
    Task<Guid> CreateAsync(AuthorizedScope scope, CampaignDraft draft, CancellationToken ct = default);
    Task<Decision> TransitionAsync(AuthorizedScope scope, Guid campaignId,
        TransitionCommand command, CancellationToken ct = default);
}

public interface IRuntimeRegistry
{
    Task<Guid> RegisterAsync(AuthorizedScope scope, RuntimeRegistration registration,
        CancellationToken ct = default);
    Task HeartbeatAsync(AuthorizedScope scope, Guid runtimeId, DateTimeOffset observedAt,
        CancellationToken ct = default);
    Task RevokeAsync(AuthorizedScope scope, Guid runtimeId, CancellationToken ct = default);
}

public interface IParticipationScheduler
{
    Task<Guid> OfferAsync(AuthorizedScope scope, OfferDraft offer, CancellationToken ct = default);
    Task<Decision> DecideAsync(AuthorizedScope scope, Guid offerId, bool accept,
        CancellationToken ct = default);
    Task<Guid> CreatePackageAsync(AuthorizedScope scope, PackageDraft draft,
        CancellationToken ct = default);
}

public interface ILeaseAuthority
{
    Task<LeaseView> AcquireAsync(AuthorizedScope scope, LeaseRequest request,
        CancellationToken ct = default);
    Task<LeaseView> RenewAsync(AuthorizedScope scope, Guid leaseId, long expectedFence,
        DateTimeOffset expiresAt, CancellationToken ct = default);
    Task ReleaseAsync(AuthorizedScope scope, Guid leaseId, long expectedFence,
        CancellationToken ct = default);
}

public interface IExternalWorkspaceProvider
{
    // Only external identity and metadata are returned; source content never crosses this port.
    Task<string> PrepareReferenceAsync(AuthorizedScope scope, Guid workPackageId,
        string baseCommitSha, CancellationToken ct = default);
}

public interface IIntegrationQueue
{
    Task<Guid> EnqueueAsync(AuthorizedScope scope, Guid changeSetId, string idempotencyKey,
        CancellationToken ct = default);
    Task<Decision> DecideAsync(AuthorizedScope scope, Guid queueItemId, long expectedFence,
        CancellationToken ct = default);
}

public interface IProjectMcpDirectory
{
    Task<string> GetServiceRefAsync(AuthorizedScope scope, CancellationToken ct = default);
    Task<IReadOnlyList<string>> DiscoverCapabilitiesAsync(AuthorizedScope scope,
        CancellationToken ct = default);
}

public interface IKillSwitchAuthority
{
    Task ActivateAsync(AuthorizedScope scope, KillSwitchTargetType type, Guid targetId,
        string reason, DateTimeOffset? expiresAt = null, CancellationToken ct = default);
    Task DeactivateAsync(AuthorizedScope scope, Guid killSwitchId, CancellationToken ct = default);
    Task<bool> IsKilledAsync(AuthorizedScope scope, KillSwitchTargetType type, Guid targetId,
        CancellationToken ct = default);
}

public interface ICoordinationChannels
{
    Task<Guid> OpenAsync(AuthorizedScope scope, string channelType, Guid? campaignId,
        Guid? workPackageId, CancellationToken ct = default);
    Task<MessageView> SendAsync(AuthorizedScope scope, Guid runtimeId, MessageDraft message,
        CancellationToken ct = default);
    Task<Page<MessageView>> ReplayAsync(AuthorizedScope scope, Guid channelId, long afterSequence,
        int limit, CancellationToken ct = default);
    Task AcknowledgeAsync(AuthorizedScope scope, Guid channelId, long processedThroughSequence,
        CancellationToken ct = default);
}

public interface IAgentHandoffs
{
    Task<Guid> ProposeAsync(AuthorizedScope scope, HandoffDraft draft, CancellationToken ct = default);
    Task<Decision> DecideAsync(AuthorizedScope scope, Guid handoffId, bool accept,
        CancellationToken ct = default);
}

public interface ICoordinationEvents
{
    Task<Page<CoordinationEventView>> ReplayAsync(AuthorizedScope scope, Guid campaignId,
        long afterSequence, int limit, CancellationToken ct = default);
}
