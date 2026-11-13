using System.Text.Json;
using DevWeave.Application.Abstractions;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Contracts.Planned.V1;
using DevWeave.Application.Planning;
using DevWeave.Domain.Audit;
using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
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
    private readonly IPlanDesignRepository _plans;
    private readonly IKnowledgeItemRepository _knowledge;
    private readonly IProjectMembershipRepository _memberships;
    private readonly IKnowledgeRevisionRepository _revisions;

    public CoordinationService(
        ICoordinationRepository repository,
        IUnitOfWork uow,
        IRepositoryReferenceAdapter git,
        PlanLockReview planLock,
        IAuditRepository audit,
        IPlanDesignRepository plans,
        IKnowledgeItemRepository knowledge,
        IProjectMembershipRepository memberships,
        IKnowledgeRevisionRepository revisions)
    {
        _repository = repository;
        _uow = uow;
        _git = git;
        _planLock = planLock;
        _audit = audit;
        _plans = plans;
        _knowledge = knowledge;
        _memberships = memberships;
        _revisions = revisions;
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
        var baseline = await RequireProjectBaselineAsync(scope, draft.BaselineId, ct);

        var campaign = ExecutionCampaign.Create(
            scope.TenantId,
            scope.ProjectId!,
            baseline.Id.Value,
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

    public async Task<BaselineLockPreview> PreviewLockAsync(AuthorizedScope scope, CancellationToken ct = default)
    {
        var preview = await _planLock.PreviewAsync(scope.TenantId, scope.ProjectId!, ct);
        var (current, next) = await GetPlanVersionsAsync(scope.TenantId, scope.ProjectId!, ct);
        var drift = await BuildDriftAsync(scope.TenantId, scope.ProjectId!, preview, ct);
        return new BaselineLockPreview(preview.CanLock, preview.Items, preview.Blockers, current, next, drift);
    }

    public async Task<Guid> LockAsync(AuthorizedScope scope, BaselineDraft draft, CancellationToken ct = default)
    {
        // Locking freezes what every agent builds against, so only a project Owner or Admin may do it.
        await Lifecycle.ProjectWorkAuthorization.EnsureMemberAsync(
            _memberships, scope.TenantId, scope.ProjectId!, scope.ActorId, reviewerOnly: true, ct);

        // The server decides what the approved plan is: the current revision of every outline item,
        // each reviewed by a human. Nothing unapproved, stale or from another project can be locked
        // (MCO-BR-01, MCO-BR-10).
        var preview = await _planLock.PreviewAsync(scope.TenantId, scope.ProjectId!, ct);
        if (!preview.CanLock)
            throw new ConflictException(preview.Describe());

        var (_, nextVersion) = await GetPlanVersionsAsync(scope.TenantId, scope.ProjectId!, ct);
        if (draft.ExpectedPlanVersion != nextVersion)
            throw new ConflictException(
                $"Expected plan version {nextVersion}, but the request supplied {draft.ExpectedPlanVersion}. Another baseline may have been locked; refresh the lock preview and try again.");

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
            nextVersion);

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
                    planVersion = nextVersion,
                    revisionCount = approved.Count,
                    revisionIds = approved,
                }),
                scope.CorrelationId),
            ct);

        // ARCH:OD-07 — freeze implementation still bound to prior baselines once a new plan version locks.
        var suspended = await SuspendCampaignsNotOnBaselineAsync(scope.TenantId, scope.ProjectId!, baseline.Id.Value, ct);

        await _uow.SaveChangesAsync(ct);

        if (suspended > 0)
        {
            await _audit.AddAsync(
                AuditRecord.Record(
                    scope.TenantId,
                    scope.ActorId,
                    "plan.campaigns.suspended_for_rebase",
                    "ExecutionCampaign",
                    baseline.Id.Value.ToString(),
                    JsonSerializer.Serialize(new { suspended, newBaselineId = baseline.Id.Value, planVersion = nextVersion }),
                    scope.CorrelationId),
                ct);
            await _uow.SaveChangesAsync(ct);
        }

        return baseline.Id.Value;
    }

    public async Task<ExportManifest> DescribeAsync(AuthorizedScope scope, Guid baselineId, CancellationToken ct = default)
    {
        var baseline = await RequireProjectBaselineAsync(scope, baselineId, ct);

        return new ExportManifest(
            scope.TenantId.Value,
            "1.0",
            baseline.CreatedAt,
            baseline.ApprovedRevisionIds.Select(id => new ResourceRef(Guid.Empty, id)).ToList(),
            new List<string>());
    }

    public async Task<IReadOnlyList<BaselineSummary>> ListBaselinesAsync(AuthorizedScope scope, CancellationToken ct = default)
    {
        var baselines = await _repository.ListBaselinesByProjectAsync(scope.TenantId, scope.ProjectId!, ct);
        return baselines
            .Select(b => new BaselineSummary(b.Id.Value, b.Goal, b.ExpectedPlanVersion, b.ApprovedRevisionIds.Count, b.CreatedAt))
            .ToList();
    }

    public async Task<int> SuspendDriftedCampaignsAsync(AuthorizedScope scope, CancellationToken ct = default)
    {
        await Lifecycle.ProjectWorkAuthorization.EnsureMemberAsync(
            _memberships, scope.TenantId, scope.ProjectId!, scope.ActorId, reviewerOnly: true, ct);

        var baselines = await _repository.ListBaselinesByProjectAsync(scope.TenantId, scope.ProjectId!, ct);
        var latest = baselines.FirstOrDefault();
        if (latest is null)
            return 0;

        var suspended = await SuspendCampaignsNotOnBaselineAsync(scope.TenantId, scope.ProjectId!, latest.Id.Value, ct);
        if (suspended > 0)
            await _uow.SaveChangesAsync(ct);
        return suspended;
    }

    private async Task<PlanBaseline> RequireProjectBaselineAsync(AuthorizedScope scope, Guid baselineId, CancellationToken ct)
    {
        var baseline = await _repository.GetBaselineByIdAsync(scope.TenantId, new BaselineId(baselineId), ct)
            ?? throw new NotFoundException("Baseline not found.");
        if (baseline.ProjectId != scope.ProjectId)
            throw new NotFoundException("Baseline was not found within this project.");
        return baseline;
    }

    private async Task<(int Current, int Next)> GetPlanVersionsAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct)
    {
        var baselines = await _repository.ListBaselinesByProjectAsync(tenantId, projectId, ct);
        var current = baselines.Count == 0 ? 0 : baselines.Max(b => b.ExpectedPlanVersion);
        return (current, current + 1);
    }

    /// <summary>
    /// Compares the living approved plan with the last locked baseline item by item. A baseline only
    /// stores revision ids, so each one is resolved back to its knowledge item: an item present in both
    /// with a different revision is "changed", one only in the living plan is "added", and one only in
    /// the baseline is "removed" (or "changed" when it is still in the outline but awaiting review).
    /// </summary>
    private async Task<PlanDriftReport> BuildDriftAsync(
        TenantId tenantId,
        ProjectId projectId,
        PlanLockPreview preview,
        CancellationToken ct)
    {
        var baselines = await _repository.ListBaselinesByProjectAsync(tenantId, projectId, ct);
        var last = baselines.FirstOrDefault();
        var campaigns = await _repository.ListCampaignsByProjectAsync(tenantId, projectId, ct);
        var activeOnPrior = last is null
            ? Array.Empty<Guid>()
            : campaigns
                .Where(c => c.Status == CampaignStatus.Active && c.BaselineId != last.Id.Value)
                .Select(c => c.Id.Value)
                .ToArray();

        // A work item counts as stale only if its newest pin for an item is behind that item's current revision.
        var staleLinks = new List<string>();
        var newestPins = (await _plans.ListLinksAsync(tenantId, projectId, ct))
            .GroupBy(l => (l.WorkItemId, l.KnowledgeItemId))
            .Select(g => g.OrderByDescending(l => l.CreatedAt).First());
        foreach (var link in newestPins)
        {
            var item = await _knowledge.GetByIdAsync(tenantId, link.KnowledgeItemId, ct);
            if (item is null || item.ProjectId != projectId)
                continue;
            if (item.CurrentRevisionId is { } current && current != link.RevisionId)
                staleLinks.Add($"{item.Key}→work");
        }
        staleLinks = staleLinks.Distinct(StringComparer.Ordinal).ToList();

        var living = preview.Items.ToDictionary(i => i.ItemId, i => i);

        if (last is null)
        {
            return new PlanDriftReport(
                HasDrift: living.Count > 0 || staleLinks.Count > 0,
                LastBaselineId: null,
                LastPlanVersion: null,
                AddedKeys: living.Values.Select(i => i.Key).OrderBy(k => k, StringComparer.Ordinal).ToList(),
                RemovedKeys: Array.Empty<string>(),
                ChangedKeys: Array.Empty<string>(),
                StaleWorkLinkKeys: staleLinks,
                ActiveCampaignIdsOnPriorBaselines: activeOnPrior);
        }

        // Resolve each locked revision to the item it belongs to.
        var locked = new Dictionary<KnowledgeItemId, Guid>();
        var unresolved = new List<string>();
        foreach (var revisionId in last.ApprovedRevisionIds)
        {
            var revision = await _revisions.GetByIdAsync(tenantId, new KnowledgeRevisionId(revisionId), ct);
            if (revision is null)
                unresolved.Add(revisionId.ToString("N")[..8]);
            else
                locked[revision.KnowledgeItemId] = revisionId;
        }

        var added = new List<string>();
        var changed = new List<string>();
        var removed = new List<string>(unresolved);

        foreach (var (itemId, entry) in living)
        {
            if (!locked.TryGetValue(itemId, out var lockedRevision))
                added.Add(entry.Key);
            else if (lockedRevision != entry.RevisionId.Value)
                changed.Add(entry.Key);
        }

        foreach (var itemId in locked.Keys.Where(id => !living.ContainsKey(id)))
        {
            var item = await _knowledge.GetByIdAsync(tenantId, itemId, ct);
            var stillInOutline = item is not null && await _plans.GetNodeAsync(tenantId, projectId, itemId, ct) is not null;
            if (stillInOutline)
                changed.Add(item!.Key); // edited and awaiting review, so not part of the approved set right now
            else
                removed.Add(item?.Key ?? itemId.Value.ToString("N")[..8]);
        }

        static List<string> Sorted(List<string> keys) => keys.Distinct(StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal).ToList();
        added = Sorted(added);
        changed = Sorted(changed);
        removed = Sorted(removed);

        var hasDrift = added.Count > 0 || changed.Count > 0 || removed.Count > 0
            || staleLinks.Count > 0 || activeOnPrior.Length > 0;

        return new PlanDriftReport(
            hasDrift,
            last.Id.Value,
            last.ExpectedPlanVersion,
            added,
            removed,
            changed,
            staleLinks,
            activeOnPrior);
    }

    private async Task<int> SuspendCampaignsNotOnBaselineAsync(
        TenantId tenantId,
        ProjectId projectId,
        Guid keepBaselineId,
        CancellationToken ct)
    {
        var campaigns = await _repository.ListCampaignsByProjectAsync(tenantId, projectId, ct);
        var count = 0;
        foreach (var campaign in campaigns)
        {
            if (campaign.Status != CampaignStatus.Active || campaign.BaselineId == keepBaselineId)
                continue;
            campaign.Suspend();
            count++;
        }

        return count;
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
        var campaign = await _repository.GetCampaignByIdAsync(scope.TenantId, new CampaignId(draft.CampaignId), ct)
            ?? throw new NotFoundException("Campaign not found.");
        if (campaign.ProjectId != scope.ProjectId)
            throw new NotFoundException("Campaign was not found within this project.");

        var baseline = await RequireProjectBaselineAsync(scope, draft.BaselineId, ct);
        if (campaign.BaselineId != baseline.Id.Value)
            throw new ConflictException(
                "Work package baseline must match the campaign baseline. Create the package under the campaign's locked plan version.");

        var package = WorkPackage.Create(
            scope.TenantId,
            scope.ProjectId!,
            campaign.Id,
            baseline.Id.Value,
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
