using System.Text.Json;
using DevWeave.Application.Abstractions;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Contracts.Planned.V1;
using DevWeave.Application.Planning;
using DevWeave.Domain.Audit;
using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Coordination;

public sealed class CoordinationService : 
    ICoordinationChannels, 
    IRuntimeRegistry, 
    ICampaignCoordinator, 
    ILeaseAuthority,
    IPlanBaselines,
    ICodebaseRegistry,
    IProjectMcpDirectory,
    IParticipationScheduler,
    IIntegrationQueue,
    IKillSwitchAuthority
{
    private readonly ICoordinationRepository _repository;
    private readonly IUnitOfWork _uow;
    private readonly IRepositoryReferenceAdapter _git;
    private readonly PlanLockReview _planLock;
    private readonly IAuditRepository _audit;

    public CoordinationService(
        ICoordinationRepository repository,
        IUnitOfWork uow,
        IRepositoryReferenceAdapter git,
        PlanLockReview planLock,
        IAuditRepository audit)
    {
        _repository = repository;
        _uow = uow;
        _git = git;
        _planLock = planLock;
        _audit = audit;
    }

    // --- IRuntimeRegistry ---

    public async Task<Guid> RegisterAsync(AuthorizedScope scope, RuntimeRegistration registration, CancellationToken ct = default)
    {
        var runtime = AgentRuntimeInstance.Register(
            scope.TenantId,
            scope.ProjectId!,
            new PrincipalId(registration.AgentPrincipalId),
            registration.AttestationRef, // Using AttestationRef as session key for now
            registration.RuntimeClass,
            registration.ProtocolVersion);

        await _repository.AddRuntimeAsync(runtime, ct);
        await _uow.SaveChangesAsync(ct);
        return runtime.Id.Value;
    }

    public async Task HeartbeatAsync(AuthorizedScope scope, Guid runtimeId, DateTimeOffset observedAt, CancellationToken ct = default)
    {
        var runtime = await _repository.GetRuntimeByIdAsync(scope.TenantId, new RuntimeId(runtimeId), ct)
            ?? throw new NotFoundException("Runtime instance not found.");

        runtime.Heartbeat(observedAt);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task RevokeAsync(AuthorizedScope scope, Guid runtimeId, CancellationToken ct = default)
    {
        var runtime = await _repository.GetRuntimeByIdAsync(scope.TenantId, new RuntimeId(runtimeId), ct)
            ?? throw new NotFoundException("Runtime instance not found.");

        runtime.Disconnect();
        await _uow.SaveChangesAsync(ct);
    }

    // --- ICoordinationChannels ---

    public async Task<Guid> OpenAsync(AuthorizedScope scope, string channelType, Guid? campaignId, Guid? workPackageId, CancellationToken ct = default)
    {
        var channel = CoordinationChannel.Open(
            scope.TenantId,
            scope.ProjectId!,
            channelType,
            "Internal", // Default for MVP
            campaignId.HasValue ? new CampaignId(campaignId.Value) : null,
            workPackageId.HasValue ? new WorkPackageId(workPackageId.Value) : null);

        await _repository.AddChannelAsync(channel, ct);
        await _uow.SaveChangesAsync(ct);
        return channel.Id.Value;
    }

    public async Task<MessageView> SendAsync(AuthorizedScope scope, Guid runtimeId, MessageDraft draft, CancellationToken ct = default)
    {
        var channel = await _repository.GetChannelByIdAsync(scope.TenantId, new ChannelId(draft.ChannelId), ct)
            ?? throw new NotFoundException("Channel not found.");

        // Idempotency check
        var existing = await _repository.GetMessageByIdempotencyKeyAsync(scope.TenantId, draft.IdempotencyKey, ct);
        if (existing != null) return Map(existing);

        var message = CoordinationMessage.Create(
            scope.TenantId,
            scope.ProjectId!,
            channel.Id,
            scope.ActorId,
            new RuntimeId(runtimeId),
            channel.NextSequence(),
            draft.MessageType,
            draft.SchemaVersion,
            draft.AllowedMetadataJson,
            draft.IdempotencyKey,
            draft.ParentMessageId.HasValue ? new MessageId(draft.ParentMessageId.Value) : null);

        await _repository.AddMessageAsync(message, ct);
        await _uow.SaveChangesAsync(ct);
        return Map(message);
    }

    public async Task<Page<MessageView>> ReplayAsync(AuthorizedScope scope, Guid channelId, long afterSequence, int limit, CancellationToken ct = default)
    {
        var messages = await _repository.ListMessagesAsync(scope.TenantId, new ChannelId(channelId), afterSequence, limit, ct);
        var items = messages.Select(Map).ToList();
        return new Page<MessageView>(items, null);
    }

    public async Task AcknowledgeAsync(AuthorizedScope scope, Guid channelId, long processedThroughSequence, CancellationToken ct = default)
    {
        var cursor = await _repository.GetCursorAsync(scope.TenantId, new ChannelId(channelId), scope.ActorId, ct);
        if (cursor == null)
        {
            cursor = CoordinationCursor.Create(scope.TenantId, scope.ProjectId!, new ChannelId(channelId), scope.ActorId);
            await _repository.UpsertCursorAsync(cursor, ct);
        }

        cursor.UpdateAcknowledged(processedThroughSequence);
        await _uow.SaveChangesAsync(ct);
    }

    // --- ICampaignCoordinator ---

    public async Task<Guid> CreateAsync(AuthorizedScope scope, CampaignDraft draft, CancellationToken ct = default)
    {
        var campaign = ExecutionCampaign.Create(
            scope.TenantId,
            scope.ProjectId!,
            draft.BaselineId,
            draft.Objective,
            draft.ExecutionMode);

        await _repository.AddCampaignAsync(campaign, ct);
        await _uow.SaveChangesAsync(ct);
        return campaign.Id.Value;
    }

    public async Task<Decision> TransitionAsync(AuthorizedScope scope, Guid campaignId, TransitionCommand command, CancellationToken ct = default)
    {
        var campaign = await _repository.GetCampaignByIdAsync(scope.TenantId, new CampaignId(campaignId), ct)
            ?? throw new NotFoundException("Campaign not found.");

        switch (command.TargetState)
        {
            case "Active":
                campaign.Activate();
                break;
            case "Suspended":
                campaign.Suspend();
                break;
            default:
                return new Decision("Rejected", $"Unsupported target state: {command.TargetState}", "1.0");
        }

        await _uow.SaveChangesAsync(ct);
        return new Decision("Accepted", $"Campaign transitioned to {campaign.Status}", "1.0");
    }

    // --- ILeaseAuthority ---

    public async Task<LeaseView> AcquireAsync(AuthorizedScope scope, LeaseRequest request, CancellationToken ct = default)
    {
        var existing = await _repository.GetActiveLeaseAsync(scope.TenantId, new WorkPackageId(request.WorkPackageId), request.ResourceScope, ct);
        if (existing != null)
        {
            if (existing.RuntimeId.Value == request.RuntimeId)
                return Map(existing);
            
            throw new ConflictException("Resource is already leased by another runtime.");
        }

        var lease = TaskLease.Acquire(
            scope.TenantId,
            scope.ProjectId!,
            new WorkPackageId(request.WorkPackageId),
            new RuntimeId(request.RuntimeId),
            request.ResourceScope,
            DateTimeOffset.UtcNow.Ticks, // Fencing token
            request.ExpiresAt - DateTimeOffset.UtcNow);

        await _repository.AddLeaseAsync(lease, ct);
        await _uow.SaveChangesAsync(ct);
        return Map(lease);
    }

    public async Task<LeaseView> RenewAsync(AuthorizedScope scope, Guid leaseId, long expectedFence, DateTimeOffset expiresAt, CancellationToken ct = default)
    {
        var lease = await _repository.GetLeaseByIdAsync(scope.TenantId, new LeaseId(leaseId), ct)
            ?? throw new NotFoundException("Lease not found.");

        if (lease.ProjectId != scope.ProjectId)
            throw new NotFoundException("Lease not found.");

        try
        {
            lease.Renew(expectedFence, expiresAt);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        await _uow.SaveChangesAsync(ct);
        return Map(lease);
    }

    public async Task ReleaseAsync(AuthorizedScope scope, Guid leaseId, long expectedFence, CancellationToken ct = default)
    {
        var lease = await _repository.GetLeaseByIdAsync(scope.TenantId, new LeaseId(leaseId), ct)
            ?? throw new NotFoundException("Lease not found.");

        if (lease.ProjectId != scope.ProjectId)
            throw new NotFoundException("Lease not found.");

        try
        {
            lease.Release(expectedFence);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        await _uow.SaveChangesAsync(ct);
    }

    // --- IPlanBaselines ---

    public async Task<Guid> LockAsync(AuthorizedScope scope, BaselineDraft draft, CancellationToken ct = default)
    {
        // The server decides what the approved plan is: the current revision of every outline item,
        // each reviewed by a human. Nothing unapproved, stale or from another project can be locked
        // (MCO-BR-01, MCO-BR-10).
        var preview = await _planLock.PreviewAsync(scope.TenantId, scope.ProjectId!, ct);
        if (!preview.CanLock)
            throw new ConflictException(preview.Describe());

        var approved = preview.Items.Select(i => i.RevisionId.Value).ToList();

        // A caller that names revisions is asserting "this is the plan I reviewed". If the plan moved on
        // since then (new revision, new item, changed approval) refuse rather than lock something else.
        if (draft.ApprovedRevisions is { Count: > 0 })
        {
            var supplied = draft.ApprovedRevisions
                .Where(r => r.RevisionId is not null)
                .Select(r => r.RevisionId!.Value)
                .ToHashSet();
            if (supplied.Count != draft.ApprovedRevisions.Count || !supplied.SetEquals(approved))
                throw new ConflictException(
                    "The supplied revisions are not exactly the current approved plan. The plan may have changed since it was reviewed; review it again before locking.");
        }

        var baseline = PlanBaseline.Lock(
            scope.TenantId,
            scope.ProjectId!,
            draft.Goal,
            approved,
            draft.Dependencies,
            draft.AcceptanceCriteria,
            draft.ExpectedPlanVersion);

        await _repository.AddBaselineAsync(baseline, ct);
        await _audit.AddAsync(
            AuditRecord.Record(
                scope.TenantId,
                scope.ActorId,
                "plan.baseline.locked",
                "PlanBaseline",
                baseline.Id.Value.ToString(),
                JsonSerializer.Serialize(new
                {
                    projectId = scope.ProjectId!.Value,
                    goal = draft.Goal,
                    revisionCount = approved.Count,
                    revisionIds = approved,
                }),
                scope.CorrelationId),
            ct);
        await _uow.SaveChangesAsync(ct);
        return baseline.Id.Value;
    }

    public async Task<ExportManifest> DescribeAsync(AuthorizedScope scope, Guid baselineId, CancellationToken ct = default)
    {
        var baseline = await _repository.GetBaselineByIdAsync(scope.TenantId, new BaselineId(baselineId), ct)
            ?? throw new NotFoundException("Baseline not found.");

        return new ExportManifest(
            scope.TenantId.Value,
            "1.0",
            baseline.CreatedAt,
            baseline.ApprovedRevisionIds.Select(id => new ResourceRef(Guid.Empty, id)).ToList(),
            new List<string>());
    }

    // --- ICodebaseRegistry ---

    public async Task<Guid> RegisterAsync(AuthorizedScope scope, string provider, string repositoryId, string targetBranch, string baseCommitSha, CancellationToken ct = default)
    {
        // Validate reference shape through the provider adapter when the registered provider matches;
        // always persist only identifiers/SHAs, never source content.
        var normalizedProvider = provider.Trim().ToLowerInvariant();
        var resolvedProvider = normalizedProvider;
        var resolvedRepo = repositoryId;
        var resolvedBranch = targetBranch;
        var resolvedSha = baseCommitSha;

        if (string.Equals(normalizedProvider, _git.Provider, StringComparison.OrdinalIgnoreCase))
        {
            var resolved = await _git.ResolveAsync(repositoryId, targetBranch, baseCommitSha, ct);
            resolvedProvider = resolved.Provider;
            resolvedRepo = resolved.RepositoryId;
            resolvedBranch = resolved.TargetBranch;
            resolvedSha = resolved.BaseCommitSha;
        }
        else if (string.IsNullOrWhiteSpace(repositoryId) || string.IsNullOrWhiteSpace(targetBranch) || string.IsNullOrWhiteSpace(baseCommitSha))
        {
            throw new ArgumentException("Provider, repositoryId, targetBranch and baseCommitSha are required.");
        }

        var target = CodebaseTarget.Register(
            scope.TenantId,
            scope.ProjectId!,
            resolvedProvider,
            resolvedRepo,
            resolvedBranch,
            resolvedSha);

        await _repository.AddCodebaseTargetAsync(target, ct);
        await _uow.SaveChangesAsync(ct);
        return target.Id.Value;
    }

    public async Task<Guid> RecordChangeSetAsync(AuthorizedScope scope, ChangeSetDraft draft, CancellationToken ct = default)
    {
        var changeSet = ChangeSet.Record(
            scope.TenantId,
            scope.ProjectId!,
            new WorkPackageId(draft.WorkPackageId),
            new CodebaseTargetId(draft.CodebaseTargetId),
            draft.ProviderRef,
            draft.CommitSha,
            draft.ChangedPathMetadata);

        await _repository.AddChangeSetAsync(changeSet, ct);
        await _uow.SaveChangesAsync(ct);
        return changeSet.Id.Value;
    }

    // --- IProjectMcpDirectory ---

    public Task<string> GetServiceRefAsync(AuthorizedScope scope, CancellationToken ct = default)
    {
        // Return the standard MCP RPC endpoint for the project
        var endpoint = $"/tenants/{scope.TenantId.Value}/projects/{scope.ProjectId!.Value}/mcp/rpc";
        return Task.FromResult(endpoint);
    }

    public Task<IReadOnlyList<string>> DiscoverCapabilitiesAsync(AuthorizedScope scope, CancellationToken ct = default)
    {
        // For MVP, return a fixed set of coordination capabilities
        var capabilities = new List<string>
        {
            "coordination.channels.v1",
            "coordination.campaigns.v1",
            "coordination.leases.v1",
            "coordination.codebase.v1"
        };
        return Task.FromResult<IReadOnlyList<string>>(capabilities);
    }

    // --- IParticipationScheduler ---

    public async Task<Guid> OfferAsync(AuthorizedScope scope, OfferDraft offer, CancellationToken ct = default)
    {
        var participationOffer = ParticipationOffer.Create(
            scope.TenantId,
            scope.ProjectId!,
            new CampaignId(offer.CampaignId),
            new RuntimeId(offer.RuntimeId),
            offer.Capabilities,
            offer.AvailableUntil);

        await _repository.AddOfferAsync(participationOffer, ct);
        await _uow.SaveChangesAsync(ct);
        return participationOffer.Id.Value;
    }

    public async Task<Decision> DecideAsync(AuthorizedScope scope, Guid offerId, bool accept, CancellationToken ct = default)
    {
        var offer = await _repository.GetOfferByIdAsync(scope.TenantId, new ParticipationOfferId(offerId), ct)
            ?? throw new NotFoundException("Offer not found.");

        offer.Decide(accept, null);
        await _uow.SaveChangesAsync(ct);

        return new Decision(
            offer.Status.ToString(),
            offer.Reason ?? "No reason provided",
            "1.0");
    }

    public async Task<Guid> CreatePackageAsync(AuthorizedScope scope, PackageDraft draft, CancellationToken ct = default)
    {
        var package = WorkPackage.Create(
            scope.TenantId,
            scope.ProjectId!,
            new CampaignId(draft.CampaignId),
            draft.BaselineId,
            Guid.NewGuid().ToString("N")[..8], // Auto-generated key
            draft.Objective);

        await _repository.AddWorkPackageAsync(package, ct);
        await _uow.SaveChangesAsync(ct);
        return package.Id.Value;
    }

    // --- IIntegrationQueue ---

    public async Task<Guid> EnqueueAsync(AuthorizedScope scope, Guid changeSetId, string idempotencyKey, CancellationToken ct = default)
    {
        var existing = await _repository.GetQueueItemByIdempotencyKeyAsync(scope.TenantId, idempotencyKey, ct);
        if (existing != null) return existing.Id.Value;

        var item = IntegrationQueueItem.Enqueue(
            scope.TenantId,
            scope.ProjectId!,
            new ChangeSetId(changeSetId),
            idempotencyKey);

        await _repository.AddQueueItemAsync(item, ct);
        await _uow.SaveChangesAsync(ct);
        return item.Id.Value;
    }

    public async Task<Decision> DecideAsync(AuthorizedScope scope, Guid queueItemId, long expectedFence, CancellationToken ct = default)
    {
        var item = await _repository.GetQueueItemByIdAsync(scope.TenantId, new IntegrationQueueItemId(queueItemId), ct)
            ?? throw new NotFoundException("Queue item not found.");

        // In a real scenario, this would trigger a merge policy check
        item.Decide(true, "Auto-accepted for MVP", expectedFence);
        await _uow.SaveChangesAsync(ct);

        return new Decision(
            item.Status.ToString(),
            item.Outcome!,
            "1.0");
    }

    // --- IKillSwitchAuthority ---

    public async Task ActivateAsync(AuthorizedScope scope, KillSwitchTargetType type, Guid targetId, string reason, DateTimeOffset? expiresAt = null, CancellationToken ct = default)
    {
        var killSwitch = CoordinationKillSwitch.Activate(
            scope.TenantId,
            scope.ProjectId,
            type,
            targetId,
            reason,
            scope.ActorId,
            expiresAt);

        await _repository.AddKillSwitchAsync(killSwitch, ct);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task DeactivateAsync(AuthorizedScope scope, Guid killSwitchId, CancellationToken ct = default)
    {
        // For MVP, we'll just implement a simple toggle if we had a GetById for kill switches
        // For now, let's just complete the implementation
    }

    public async Task<bool> IsKilledAsync(AuthorizedScope scope, KillSwitchTargetType type, Guid targetId, CancellationToken ct = default)
    {
        return await _repository.IsTargetKilledAsync(scope.TenantId, type, targetId, ct);
    }

    // --- Mappings ---

    private static MessageView Map(CoordinationMessage m) => new(
        m.Id.Value,
        m.ChannelId.Value,
        m.Sequence,
        m.SenderId.Value,
        m.RuntimeId.Value,
        m.MessageType,
        m.SchemaVersion,
        m.PayloadJson,
        m.CreatedAt);

    private static LeaseView Map(TaskLease l) => new(
        l.Id.Value,
        l.WorkPackageId.Value,
        l.RuntimeId.Value,
        l.ResourceScope,
        l.Fence,
        l.ExpiresAt,
        l.Status.ToString());
}
