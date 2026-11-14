using DevWeave.Api.Security;
using DevWeave.Application.Abstractions;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Application.Planning;
using DevWeave.Contracts.Planned.V1;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using Microsoft.AspNetCore.Mvc;

namespace DevWeave.Api.Endpoints;

/// <summary>
/// REST surface for plan baselines, campaigns, runtimes, leases, channels, codebase targets,
/// change sets and integration queue (SRS MCO / MCP coordination plane).
/// </summary>
public static class CoordinationEndpoints
{
    public static IEndpointRouteBuilder MapCoordinationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/tenants/{tenantId:guid}/projects/{projectId:guid}/coordination")
            .RequireAuthorization();

        group.MapGet("/baselines/preview", PreviewBaselineAsync)
            .RequireAuthorization(ApiAuthorizationPolicies.Human);
        group.MapGet("/baselines", ListBaselinesAsync)
            .RequireAuthorization(ApiAuthorizationPolicies.Human);
        group.MapPost("/baselines", LockBaselineAsync)
            .RequireAuthorization(ApiAuthorizationPolicies.Human);
        group.MapGet("/baselines/{baselineId:guid}", DescribeBaselineAsync);
        group.MapPost("/campaigns/suspend-drifted", SuspendDriftedCampaignsAsync)
            .RequireAuthorization(ApiAuthorizationPolicies.Human);

        group.MapPost("/codebase-targets", RegisterCodebaseTargetAsync);
        group.MapPost("/change-sets", RecordChangeSetAsync);

        group.MapPost("/campaigns", CreateCampaignAsync)
            .RequireAuthorization(ApiAuthorizationPolicies.Human);
        group.MapGet("/campaigns", ListCampaignsAsync);
        group.MapPost("/campaigns/{campaignId:guid}/transition", TransitionCampaignAsync)
            .RequireAuthorization(ApiAuthorizationPolicies.Human);
        group.MapGet("/campaigns/{campaignId:guid}/packages", ListPackagesAsync);
        group.MapGet("/plan-status", GetPlanStatusAsync)
            .RequireAuthorization(ApiAuthorizationPolicies.Human);
        group.MapGet("/packages/{workPackageId:guid}/status", GetWorkStatusAsync)
            .RequireAuthorization(ApiAuthorizationPolicies.Human);

        group.MapPost("/runtimes", RegisterRuntimeAsync)
            .RequireAuthorization(ApiAuthorizationPolicies.Agent);
        group.MapGet("/runtimes", ListRuntimesAsync);
        group.MapPost("/runtimes/{runtimeId:guid}/heartbeat", HeartbeatRuntimeAsync)
            .RequireAuthorization(ApiAuthorizationPolicies.Agent);
        group.MapPost("/runtimes/{runtimeId:guid}/revoke", RevokeRuntimeAsync);

        group.MapPost("/packages", CreatePackageAsync);
        group.MapPost("/offers", OfferParticipationAsync);
        group.MapPost("/offers/{offerId:guid}/decide", DecideOfferAsync);

        group.MapPost("/leases", AcquireLeaseAsync)
            .RequireAuthorization(ApiAuthorizationPolicies.Agent);
        group.MapGet("/leases", ListActiveLeasesAsync)
            .RequireAuthorization(ApiAuthorizationPolicies.Human);
        group.MapPost("/leases/{leaseId:guid}/renew", RenewLeaseAsync)
            .RequireAuthorization(ApiAuthorizationPolicies.Agent);
        group.MapPost("/leases/{leaseId:guid}/release", ReleaseLeaseAsync)
            .RequireAuthorization(ApiAuthorizationPolicies.Agent);

        group.MapPost("/channels", OpenChannelAsync);
        group.MapGet("/channels", ListChannelsAsync)
            .RequireAuthorization(ApiAuthorizationPolicies.Human);
        group.MapPost("/channels/{channelId:guid}/messages", SendMessageAsync)
            .RequireAuthorization(ApiAuthorizationPolicies.Agent);
        group.MapGet("/channels/{channelId:guid}/messages", ReplayMessagesAsync);
        group.MapPost("/channels/{channelId:guid}/ack", AcknowledgeAsync)
            .RequireAuthorization(ApiAuthorizationPolicies.Agent);

        group.MapPost("/integration-queue", EnqueueIntegrationAsync);
        group.MapPost("/integration-queue/{queueItemId:guid}/decide", DecideIntegrationAsync);

        group.MapGet("/mcp", GetMcpDirectoryAsync);

        group.MapPost("/workspace-refs", PrepareWorkspaceRefAsync);

        return endpoints;
    }

    private static AuthorizedScope Scope(Guid tenantId, Guid projectId, HttpContext http, string purpose) =>
        new(new TenantId(tenantId), AuthenticatedPrincipal.GetId(http), new ProjectId(projectId), purpose, http.TraceIdentifier);

    private static async Task<IResult> PreviewBaselineAsync(
        Guid tenantId, Guid projectId, IPlanBaselines baselines, HttpContext http, CancellationToken ct)
    {
        var preview = await baselines.PreviewLockAsync(Scope(tenantId, projectId, http, "Preview plan baseline"), ct);
        return Results.Ok(new PlanLockPreviewResponse(
            preview.CanLock,
            preview.Items.Select(i => new PlanLockItemResponse(
                i.ItemId.Value, i.Key, i.Title, i.TypeId, i.RevisionId.Value, i.RevisionNumber, i.ParentKey)).ToList(),
            preview.Blockers.Select(b => new PlanLockBlockerResponse(b.Key, b.Reason)).ToList(),
            preview.CurrentPlanVersion,
            preview.NextPlanVersion,
            new PlanDriftResponse(
                preview.Drift.HasDrift,
                preview.Drift.LastBaselineId,
                preview.Drift.LastPlanVersion,
                preview.Drift.AddedKeys,
                preview.Drift.RemovedKeys,
                preview.Drift.ChangedKeys,
                preview.Drift.StaleWorkLinkKeys,
                preview.Drift.ActiveCampaignIdsOnPriorBaselines)));
    }

    private static async Task<IResult> ListBaselinesAsync(
        Guid tenantId, Guid projectId, IPlanBaselines baselines, HttpContext http, CancellationToken ct)
    {
        var list = await baselines.ListBaselinesAsync(Scope(tenantId, projectId, http, "List plan baselines"), ct);
        return Results.Ok(list.Select(b => new
        {
            id = b.Id,
            goal = b.Goal,
            planVersion = b.PlanVersion,
            revisionCount = b.RevisionCount,
            createdAt = b.CreatedAt,
        }));
    }

    private static async Task<IResult> LockBaselineAsync(
        Guid tenantId, Guid projectId, [FromBody] BaselineDraft draft,
        IPlanBaselines baselines, HttpContext http, CancellationToken ct)
    {
        try
        {
            var id = await baselines.LockAsync(Scope(tenantId, projectId, http, "Lock plan baseline"), draft, ct);
            return Results.Created($"/tenants/{tenantId}/projects/{projectId}/coordination/baselines/{id}", new { id });
        }
        catch (NotFoundException) { return Results.NotFound(); }
        catch (ConflictException ex) { return Results.Conflict(new { error = ex.Message }); }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
    }

    private static async Task<IResult> DescribeBaselineAsync(
        Guid tenantId, Guid projectId, Guid baselineId,
        IPlanBaselines baselines, HttpContext http, CancellationToken ct)
    {
        try
        {
            var manifest = await baselines.DescribeAsync(Scope(tenantId, projectId, http, "Describe baseline"), baselineId, ct);
            return Results.Ok(manifest);
        }
        catch (NotFoundException) { return Results.NotFound(); }
    }

    private static async Task<IResult> SuspendDriftedCampaignsAsync(
        Guid tenantId, Guid projectId, IPlanBaselines baselines, HttpContext http, CancellationToken ct)
    {
        try
        {
            var suspended = await baselines.SuspendDriftedCampaignsAsync(
                Scope(tenantId, projectId, http, "Suspend campaigns on prior plan baselines"), ct);
            return Results.Ok(new { suspended });
        }
        catch (NotFoundException) { return Results.NotFound(); }
        catch (ConflictException ex) { return Results.Conflict(new { error = ex.Message }); }
    }

    private static async Task<IResult> RegisterCodebaseTargetAsync(
        Guid tenantId, Guid projectId, [FromBody] CodebaseTargetDraft draft,
        ICodebaseRegistry registry, HttpContext http, CancellationToken ct)
    {
        try
        {
            var id = await registry.RegisterAsync(
                Scope(tenantId, projectId, http, "Register codebase target"),
                draft.Provider, draft.RepositoryId, draft.TargetBranch, draft.BaseCommitSha, ct);
            return Results.Created($"/tenants/{tenantId}/projects/{projectId}/coordination/codebase-targets/{id}", new { id });
        }
        catch (NotFoundException) { return Results.NotFound(); }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
    }

    private static async Task<IResult> RecordChangeSetAsync(
        Guid tenantId, Guid projectId, [FromBody] ChangeSetDraft draft,
        ICodebaseRegistry registry, HttpContext http, CancellationToken ct)
    {
        try
        {
            var id = await registry.RecordChangeSetAsync(Scope(tenantId, projectId, http, "Record change set"), draft, ct);
            return Results.Created($"/tenants/{tenantId}/projects/{projectId}/coordination/change-sets/{id}", new { id });
        }
        catch (NotFoundException) { return Results.NotFound(); }
        catch (ConflictException ex) { return Results.Conflict(new { error = ex.Message }); }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
    }

    private static async Task<IResult> CreateCampaignAsync(
        Guid tenantId, Guid projectId, [FromBody] CampaignDraft draft,
        ICampaignCoordinator coordinator, HttpContext http, CancellationToken ct)
    {
        try
        {
            var id = await coordinator.CreateAsync(Scope(tenantId, projectId, http, "Create campaign"), draft, ct);
            return Results.Created($"/tenants/{tenantId}/projects/{projectId}/coordination/campaigns/{id}", new { id });
        }
        catch (NotFoundException) { return Results.NotFound(); }
        catch (ConflictException ex) { return Results.Conflict(new { error = ex.Message }); }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
    }

    private static async Task<IResult> ListCampaignsAsync(
        Guid tenantId, Guid projectId, ICoordinationRepository repository, CancellationToken ct)
    {
        var campaigns = await repository.ListCampaignsByProjectAsync(
            new TenantId(tenantId), new ProjectId(projectId), ct);
        return Results.Ok(campaigns.Select(c => new
        {
            id = c.Id.Value,
            baselineId = c.BaselineId,
            objective = c.Objective,
            executionMode = c.ExecutionMode,
            status = c.Status.ToString(),
            version = c.Version,
            createdAt = c.CreatedAt,
            updatedAt = c.UpdatedAt,
        }));
    }

    private static async Task<IResult> ListPackagesAsync(
        Guid tenantId, Guid projectId, Guid campaignId, IWorkStatusReader status, HttpContext http, CancellationToken ct)
    {
        try
        {
            var packages = await status.ListWorkPackagesAsync(
                Scope(tenantId, projectId, http, "List work packages"), campaignId, ct);
            return Results.Ok(packages.Select(MapWorkStatus));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> GetPlanStatusAsync(
        Guid tenantId, Guid projectId, IWorkStatusReader status, HttpContext http, CancellationToken ct)
    {
        var plan = await status.GetPlanStatusAsync(Scope(tenantId, projectId, http, "Get plan status"), ct);
        return Results.Ok(new
        {
            currentPlanVersion = plan.CurrentPlanVersion,
            currentBaselineId = plan.CurrentBaselineId,
            lockedAt = plan.LockedAt,
            campaigns = plan.Campaigns.Select(c => new
            {
                id = c.Id,
                baselineId = c.BaselineId,
                planVersion = c.PlanVersion,
                onCurrentPlan = c.OnCurrentPlan,
                status = c.Status,
                objective = c.Objective,
            }),
        });
    }

    private static async Task<IResult> GetWorkStatusAsync(
        Guid tenantId, Guid projectId, Guid workPackageId, IWorkStatusReader status, HttpContext http, CancellationToken ct)
    {
        try
        {
            var view = await status.GetWorkStatusAsync(Scope(tenantId, projectId, http, "Get work status"), workPackageId, ct);
            return Results.Ok(MapWorkStatus(view));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static object MapWorkStatus(WorkPackageStatusView p) => new
    {
        id = p.Id,
        campaignId = p.CampaignId,
        baselineId = p.BaselineId,
        planVersion = p.PlanVersion,
        onCurrentPlan = p.OnCurrentPlan,
        key = p.Key,
        objective = p.Objective,
        status = p.Status,
        canProgress = p.CanProgress,
        blockedReason = p.BlockedReason,
        implementsRevisionIds = p.ImplementsRevisionIds,
        workItemIds = p.WorkItemIds,
        specTopics = p.SpecTopics,
        unscoped = p.Unscoped,
        latestWorkthrough = p.LatestWorkthrough is null ? null : new
        {
            id = p.LatestWorkthrough.Id,
            workPackageId = p.LatestWorkthrough.WorkPackageId,
            recordedBy = p.LatestWorkthrough.RecordedBy,
            kind = p.LatestWorkthrough.Kind,
            outcome = p.LatestWorkthrough.Outcome,
            whatSucceeded = p.LatestWorkthrough.WhatSucceeded,
            blockedReason = p.LatestWorkthrough.BlockedReason,
            steps = p.LatestWorkthrough.Steps,
            changeSetId = p.LatestWorkthrough.ChangeSetId,
            recordedAt = p.LatestWorkthrough.RecordedAt,
        },
    };

    private static async Task<IResult> ListRuntimesAsync(
        Guid tenantId, Guid projectId, ICoordinationRepository repository, CancellationToken ct)
    {
        var runtimes = await repository.ListRuntimesByProjectAsync(
            new TenantId(tenantId), new ProjectId(projectId), ct);
        return Results.Ok(runtimes.Select(r => new
        {
            id = r.Id.Value,
            principalId = r.PrincipalId.Value,
            runtimeClass = r.RuntimeClass,
            protocolVersion = r.ProtocolVersion,
            status = r.Status.ToString(),
            lastHeartbeatAt = r.LastHeartbeatAt,
            createdAt = r.CreatedAt,
        }));
    }

    private static async Task<IResult> TransitionCampaignAsync(
        Guid tenantId, Guid projectId, Guid campaignId, [FromBody] TransitionCommand command,
        ICampaignCoordinator coordinator, HttpContext http, CancellationToken ct)
    {
        try
        {
            var decision = await coordinator.TransitionAsync(
                Scope(tenantId, projectId, http, "Transition campaign"), campaignId, command, ct);
            return Results.Ok(decision);
        }
        catch (NotFoundException) { return Results.NotFound(); }
        catch (ConflictException ex) { return Results.Conflict(new { error = ex.Message }); }
    }

    private static async Task<IResult> RegisterRuntimeAsync(
        Guid tenantId, Guid projectId, [FromBody] RuntimeRegistration registration,
        IRuntimeRegistry registry, HttpContext http, CancellationToken ct)
    {
        try
        {
            var id = await registry.RegisterAsync(Scope(tenantId, projectId, http, "Register runtime"), registration, ct);
            return Results.Created($"/tenants/{tenantId}/projects/{projectId}/coordination/runtimes/{id}", new { id });
        }
        catch (NotFoundException) { return Results.NotFound(); }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
    }

    private static async Task<IResult> HeartbeatRuntimeAsync(
        Guid tenantId, Guid projectId, Guid runtimeId,
        IRuntimeRegistry registry, HttpContext http, CancellationToken ct)
    {
        try
        {
            await registry.HeartbeatAsync(Scope(tenantId, projectId, http, "Runtime heartbeat"), runtimeId, DateTimeOffset.UtcNow, ct);
            return Results.NoContent();
        }
        catch (NotFoundException) { return Results.NotFound(); }
    }

    private static async Task<IResult> RevokeRuntimeAsync(
        Guid tenantId, Guid projectId, Guid runtimeId,
        IRuntimeRegistry registry, HttpContext http, CancellationToken ct)
    {
        try
        {
            await registry.RevokeAsync(Scope(tenantId, projectId, http, "Revoke runtime"), runtimeId, ct);
            return Results.NoContent();
        }
        catch (NotFoundException) { return Results.NotFound(); }
    }

    private static async Task<IResult> CreatePackageAsync(
        Guid tenantId, Guid projectId, [FromBody] PackageDraft draft,
        IParticipationScheduler scheduler, HttpContext http, CancellationToken ct)
    {
        try
        {
            var id = await scheduler.CreatePackageAsync(Scope(tenantId, projectId, http, "Create work package"), draft, ct);
            return Results.Created($"/tenants/{tenantId}/projects/{projectId}/coordination/packages/{id}", new { id });
        }
        catch (NotFoundException) { return Results.NotFound(); }
        catch (ConflictException ex) { return Results.Conflict(new { error = ex.Message }); }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
    }

    private static async Task<IResult> OfferParticipationAsync(
        Guid tenantId, Guid projectId, [FromBody] OfferDraft draft,
        IParticipationScheduler scheduler, HttpContext http, CancellationToken ct)
    {
        try
        {
            var id = await scheduler.OfferAsync(Scope(tenantId, projectId, http, "Offer participation"), draft, ct);
            return Results.Created($"/tenants/{tenantId}/projects/{projectId}/coordination/offers/{id}", new { id });
        }
        catch (NotFoundException) { return Results.NotFound(); }
    }

    private static async Task<IResult> DecideOfferAsync(
        Guid tenantId, Guid projectId, Guid offerId, [FromBody] DecideRequest request,
        IParticipationScheduler scheduler, HttpContext http, CancellationToken ct)
    {
        try
        {
            var decision = await scheduler.DecideAsync(
                Scope(tenantId, projectId, http, "Decide participation"), offerId, request.Accept, ct);
            return Results.Ok(decision);
        }
        catch (NotFoundException) { return Results.NotFound(); }
    }

    private static async Task<IResult> ListActiveLeasesAsync(
        Guid tenantId, Guid projectId, ICoordinationRepository repository, CancellationToken ct)
    {
        var leases = await repository.ListActiveLeasesByProjectAsync(
            new TenantId(tenantId), new ProjectId(projectId), ct);
        return Results.Ok(leases.Select(l => new
        {
            id = l.Id.Value,
            workPackageId = l.WorkPackageId.Value,
            runtimeId = l.RuntimeId.Value,
            resourceScope = l.ResourceScope,
            fence = l.Fence,
            expiresAt = l.ExpiresAt,
            state = l.Status.ToString(),
            createdAt = l.CreatedAt,
        }));
    }

    private static async Task<IResult> ListChannelsAsync(
        Guid tenantId, Guid projectId, ICoordinationRepository repository, CancellationToken ct)
    {
        var channels = await repository.ListChannelsByProjectAsync(
            new TenantId(tenantId), new ProjectId(projectId), ct);
        return Results.Ok(channels
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                id = c.Id.Value,
                campaignId = c.CampaignId?.Value,
                workPackageId = c.WorkPackageId?.Value,
                channelType = c.ChannelType,
                visibility = c.Visibility,
                sequenceHighWatermark = c.SequenceHighWatermark,
                createdAt = c.CreatedAt,
            }));
    }

    private static async Task<IResult> AcquireLeaseAsync(
        Guid tenantId, Guid projectId, [FromBody] LeaseRequest request,
        ILeaseAuthority leases, HttpContext http, CancellationToken ct)
    {
        try
        {
            var lease = await leases.AcquireAsync(Scope(tenantId, projectId, http, "Acquire lease"), request, ct);
            return Results.Created($"/tenants/{tenantId}/projects/{projectId}/coordination/leases/{lease.Id}", lease);
        }
        catch (NotFoundException) { return Results.NotFound(); }
        catch (ConflictException ex) { return Results.Conflict(new { error = ex.Message }); }
    }

    private static async Task<IResult> RenewLeaseAsync(
        Guid tenantId, Guid projectId, Guid leaseId, [FromBody] RenewLeaseRequest request,
        ILeaseAuthority leases, HttpContext http, CancellationToken ct)
    {
        try
        {
            var lease = await leases.RenewAsync(
                Scope(tenantId, projectId, http, "Renew lease"), leaseId, request.ExpectedFence, request.ExpiresAt, ct);
            return Results.Ok(lease);
        }
        catch (NotFoundException) { return Results.NotFound(); }
        catch (ConflictException ex) { return Results.Conflict(new { error = ex.Message }); }
    }

    private static async Task<IResult> ReleaseLeaseAsync(
        Guid tenantId, Guid projectId, Guid leaseId, [FromBody] ReleaseLeaseRequest request,
        ILeaseAuthority leases, HttpContext http, CancellationToken ct)
    {
        try
        {
            await leases.ReleaseAsync(
                Scope(tenantId, projectId, http, "Release lease"), leaseId, request.ExpectedFence, ct);
            return Results.NoContent();
        }
        catch (NotFoundException) { return Results.NotFound(); }
        catch (ConflictException ex) { return Results.Conflict(new { error = ex.Message }); }
    }

    private static async Task<IResult> OpenChannelAsync(
        Guid tenantId, Guid projectId, [FromBody] ChannelDraft draft,
        ICoordinationChannels channels, HttpContext http, CancellationToken ct)
    {
        try
        {
            var id = await channels.OpenAsync(
                Scope(tenantId, projectId, http, "Open channel"),
                draft.ChannelType, draft.CampaignId, draft.WorkPackageId, ct);
            return Results.Created($"/tenants/{tenantId}/projects/{projectId}/coordination/channels/{id}", new { id });
        }
        catch (NotFoundException) { return Results.NotFound(); }
    }

    private static async Task<IResult> SendMessageAsync(
        Guid tenantId, Guid projectId, Guid channelId,
        [FromHeader(Name = "X-Runtime-Id")] Guid runtimeId,
        [FromBody] MessageDraft draft,
        ICoordinationChannels channels, HttpContext http, CancellationToken ct)
    {
        try
        {
            var effective = draft with { ChannelId = channelId };
            var message = await channels.SendAsync(
                Scope(tenantId, projectId, http, "Send coordination message"), runtimeId, effective, ct);
            return Results.Ok(message);
        }
        catch (NotFoundException) { return Results.NotFound(); }
        catch (ConflictException ex) { return Results.Conflict(new { error = ex.Message }); }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
    }

    private static async Task<IResult> ReplayMessagesAsync(
        Guid tenantId, Guid projectId, Guid channelId,
        [FromQuery] long afterSequence, [FromQuery] int limit,
        ICoordinationChannels channels, HttpContext http, CancellationToken ct)
    {
        try
        {
            var page = await channels.ReplayAsync(
                Scope(tenantId, projectId, http, "Replay messages"),
                channelId, afterSequence, limit <= 0 ? 50 : Math.Min(limit, 200), ct);
            return Results.Ok(page);
        }
        catch (NotFoundException) { return Results.NotFound(); }
    }

    private static async Task<IResult> AcknowledgeAsync(
        Guid tenantId, Guid projectId, Guid channelId, [FromBody] CursorDraft draft,
        ICoordinationChannels channels, HttpContext http, CancellationToken ct)
    {
        try
        {
            await channels.AcknowledgeAsync(
                Scope(tenantId, projectId, http, "Acknowledge messages"),
                channelId, draft.ProcessedThroughSequence, ct);
            return Results.NoContent();
        }
        catch (NotFoundException) { return Results.NotFound(); }
    }

    private static async Task<IResult> EnqueueIntegrationAsync(
        Guid tenantId, Guid projectId, [FromBody] QueueDraft draft,
        IIntegrationQueue queue, HttpContext http, CancellationToken ct)
    {
        try
        {
            var id = await queue.EnqueueAsync(
                Scope(tenantId, projectId, http, "Enqueue integration"),
                draft.ChangeSetId, draft.IdempotencyKey, ct);
            return Results.Created($"/tenants/{tenantId}/projects/{projectId}/coordination/integration-queue/{id}", new { id });
        }
        catch (NotFoundException) { return Results.NotFound(); }
        catch (ConflictException ex) { return Results.Conflict(new { error = ex.Message }); }
    }

    private static async Task<IResult> DecideIntegrationAsync(
        Guid tenantId, Guid projectId, Guid queueItemId, [FromBody] DecideIntegrationRequest request,
        IIntegrationQueue queue, HttpContext http, CancellationToken ct)
    {
        try
        {
            var decision = await queue.DecideAsync(
                Scope(tenantId, projectId, http, "Decide integration"),
                queueItemId, request.ExpectedFence, ct);
            return Results.Ok(decision);
        }
        catch (NotFoundException) { return Results.NotFound(); }
        catch (ConflictException ex) { return Results.Conflict(new { error = ex.Message }); }
    }

    private static async Task<IResult> GetMcpDirectoryAsync(
        Guid tenantId, Guid projectId, IProjectMcpDirectory directory, HttpContext http, CancellationToken ct)
    {
        var scope = Scope(tenantId, projectId, http, "MCP directory");
        var serviceRef = await directory.GetServiceRefAsync(scope, ct);
        var capabilities = await directory.DiscoverCapabilitiesAsync(scope, ct);
        return Results.Ok(new { serviceRef, capabilities });
    }

    private static async Task<IResult> PrepareWorkspaceRefAsync(
        Guid tenantId, Guid projectId, [FromBody] WorkspaceDraft draft,
        IExternalWorkspaceProvider workspaces, HttpContext http, CancellationToken ct)
    {
        try
        {
            var reference = await workspaces.PrepareReferenceAsync(
                Scope(tenantId, projectId, http, "Prepare workspace reference"),
                draft.WorkPackageId, draft.BaseCommitSha, ct);
            return Results.Ok(new { reference });
        }
        catch (NotFoundException) { return Results.NotFound(); }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
    }

    private sealed record DecideRequest(bool Accept);
    private sealed record RenewLeaseRequest(long ExpectedFence, DateTimeOffset ExpiresAt);
    private sealed record ReleaseLeaseRequest(long ExpectedFence);
    private sealed record DecideIntegrationRequest(long ExpectedFence);
}

public sealed record PlanLockItemResponse(
    Guid ItemId, string Key, string Title, string TypeId, Guid RevisionId, int RevisionNumber, string? ParentKey);
public sealed record PlanLockBlockerResponse(string Key, string Reason);
public sealed record PlanDriftResponse(
    bool HasDrift,
    Guid? LastBaselineId,
    int? LastPlanVersion,
    IReadOnlyList<string> AddedKeys,
    IReadOnlyList<string> RemovedKeys,
    IReadOnlyList<string> ChangedKeys,
    IReadOnlyList<string> StaleWorkLinkKeys,
    IReadOnlyList<Guid> ActiveCampaignIdsOnPriorBaselines);
public sealed record PlanLockPreviewResponse(
    bool CanLock,
    IReadOnlyList<PlanLockItemResponse> Items,
    IReadOnlyList<PlanLockBlockerResponse> Blockers,
    int CurrentPlanVersion,
    int NextPlanVersion,
    PlanDriftResponse Drift);
