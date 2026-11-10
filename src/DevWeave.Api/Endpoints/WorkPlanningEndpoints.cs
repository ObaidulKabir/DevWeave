using System.Security.Claims;
using DevWeave.Api.Security;
using DevWeave.Application.Abstractions;
using DevWeave.Application.Lifecycle;
using DevWeave.Contracts.Lifecycle.V1;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;

namespace DevWeave.Api.Endpoints;

public static class WorkPlanningEndpoints
{
    public static IEndpointRouteBuilder MapWorkPlanningEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/tenants/{tenantId:guid}/projects/{projectId:guid}")
            .RequireAuthorization(ApiAuthorizationPolicies.Human);

        group.MapPost("/milestones", CreateMilestoneAsync);
        group.MapGet("/milestones", ListMilestonesAsync);
        group.MapPatch("/milestones/{milestoneId:guid}/state", ChangeMilestoneStateAsync);
        group.MapGet("/milestones/{milestoneId:guid}/transitions", ListMilestoneTransitionsAsync);
        group.MapPost("/work-items", CreateWorkItemAsync);
        group.MapGet("/work-items", ListWorkItemsAsync);
        group.MapPatch("/work-items/{itemId:guid}/planned-state", ChangePlannedStateAsync);
        group.MapPost("/work-items/{itemId:guid}/report", ReportWorkAsync);
        group.MapPost("/work-items/{itemId:guid}/verify", VerifyWorkAsync);
        group.MapPost("/work-items/{itemId:guid}/approve", ApproveWorkAsync);
        group.MapPost("/work-items/{itemId:guid}/reject-approval", RejectApprovalAsync);
        group.MapGet("/work-items/{itemId:guid}/transitions", ListTransitionsAsync);
        return endpoints;
    }

    private static async Task<IResult> CreateMilestoneAsync(Guid tenantId, Guid projectId,
        CreateMilestoneRequest request, HttpContext http, CreateMilestone useCase, CancellationToken ct)
    {
        if (!TryActor(http.User, tenantId, out var actor))
            return Results.NotFound();
        try
        {
            var milestone = await useCase.ExecuteAsync(new TenantId(tenantId), new ProjectId(projectId),
                request.Key, request.Title, request.DueAt, actor, ct);
            return Results.Created($"/tenants/{tenantId}/projects/{projectId}/milestones/{milestone.Id}",
                Map(milestone));
        }
        catch (NotFoundException) { return Results.NotFound(); }
        catch (ConflictException) { return Results.Conflict(); }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
    }

    private static async Task<IResult> ListMilestonesAsync(Guid tenantId, Guid projectId,
        HttpContext http, ListMilestones useCase, CancellationToken ct)
    {
        if (!TryActor(http.User, tenantId, out var actor))
            return Results.NotFound();
        try
        {
            var milestones = await useCase.ExecuteAsync(new TenantId(tenantId), new ProjectId(projectId), actor, ct);
            return Results.Ok(milestones.Select(Map).ToArray());
        }
        catch (NotFoundException) { return Results.NotFound(); }
    }

    private static async Task<IResult> ChangeMilestoneStateAsync(Guid tenantId, Guid projectId,
        Guid milestoneId, TransitionMilestoneRequest request, HttpContext http,
        TransitionMilestone useCase, CancellationToken ct)
    {
        if (!TryActor(http.User, tenantId, out var actor))
            return Results.NotFound();
        try
        {
            var transition = await useCase.ExecuteAsync(new TenantId(tenantId), new ProjectId(projectId),
                new MilestoneId(milestoneId), request.State, actor, request.Reason, request.ExpectedVersion, ct);
            return Results.Ok(Map(transition));
        }
        catch (NotFoundException) { return Results.NotFound(); }
        catch (ConflictException) { return Results.Conflict(); }
        catch (WorkItemConcurrencyException) { return Results.Conflict(); }
        catch (InvalidOperationException) { return Results.Conflict(); }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
    }

    private static async Task<IResult> ListMilestoneTransitionsAsync(Guid tenantId, Guid projectId,
        Guid milestoneId, HttpContext http, ListMilestoneTransitions useCase, CancellationToken ct)
    {
        if (!TryActor(http.User, tenantId, out var actor))
            return Results.NotFound();
        try
        {
            var transitions = await useCase.ExecuteAsync(new TenantId(tenantId), new ProjectId(projectId),
                new MilestoneId(milestoneId), actor, ct);
            return Results.Ok(transitions.Select(Map).ToArray());
        }
        catch (NotFoundException) { return Results.NotFound(); }
    }

    private static async Task<IResult> CreateWorkItemAsync(Guid tenantId, Guid projectId,
        CreateWorkItemRequest request, HttpContext http, CreateWorkItem useCase, CancellationToken ct)
    {
        if (!TryActor(http.User, tenantId, out var actor))
            return Results.NotFound();
        try
        {
            var item = await useCase.ExecuteAsync(new TenantId(tenantId), new ProjectId(projectId),
                request.Key, request.Title, request.Description,
                request.MilestoneId is Guid milestoneId ? new MilestoneId(milestoneId) : null,
                request.OwnerId is Guid ownerId ? new PrincipalId(ownerId) : null,
                request.DueAt, request.Priority, actor,
                request.DependsOn.Select(id => new WorkItemId(id)).ToArray(), ct);
            return Results.Created($"/tenants/{tenantId}/projects/{projectId}/work-items/{item.Id}",
                Map(item, request.DependsOn));
        }
        catch (NotFoundException) { return Results.NotFound(); }
        catch (ConflictException) { return Results.Conflict(); }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
    }

    private static async Task<IResult> ListWorkItemsAsync(Guid tenantId, Guid projectId,
        HttpContext http, ListWorkItems useCase, CancellationToken ct)
    {
        if (!TryActor(http.User, tenantId, out var actor))
            return Results.NotFound();
        try
        {
            var items = await useCase.ExecuteAsync(new TenantId(tenantId), new ProjectId(projectId), actor, ct);
            return Results.Ok(items.Select(detail => Map(detail.Item,
                detail.DependsOn.Select(id => id.Value))).ToArray());
        }
        catch (NotFoundException) { return Results.NotFound(); }
    }

    private static Task<IResult> ChangePlannedStateAsync(Guid tenantId, Guid projectId, Guid itemId,
        UpdatePlannedStateRequest request, HttpContext http, TransitionWorkItem useCase, CancellationToken ct) =>
        ExecuteTransitionAsync(http, tenantId, () => useCase.ChangePlannedAsync(
            new TenantId(tenantId), new ProjectId(projectId), new WorkItemId(itemId), request.State,
            Actor(http.User), request.Reason, request.ExpectedVersion, ct));

    private static Task<IResult> ReportWorkAsync(Guid tenantId, Guid projectId, Guid itemId,
        ReportWorkRequest request, HttpContext http, TransitionWorkItem useCase, CancellationToken ct) =>
        ExecuteTransitionAsync(http, tenantId, () => useCase.ReportAsync(
            new TenantId(tenantId), new ProjectId(projectId), new WorkItemId(itemId), request.State,
            Actor(http.User), request.Reason, request.ExpectedVersion, ct));

    private static Task<IResult> VerifyWorkAsync(Guid tenantId, Guid projectId, Guid itemId,
        ReviewWorkRequest request, HttpContext http, TransitionWorkItem useCase, CancellationToken ct) =>
        ExecuteTransitionAsync(http, tenantId, () => useCase.VerifyAsync(
            new TenantId(tenantId), new ProjectId(projectId), new WorkItemId(itemId),
            Actor(http.User), request.Reason, request.ExpectedVersion, ct));

    private static Task<IResult> ApproveWorkAsync(Guid tenantId, Guid projectId, Guid itemId,
        ReviewWorkRequest request, HttpContext http, TransitionWorkItem useCase, CancellationToken ct) =>
        ExecuteTransitionAsync(http, tenantId, () => useCase.ApproveAsync(
            new TenantId(tenantId), new ProjectId(projectId), new WorkItemId(itemId),
            Actor(http.User), request.Reason, request.ExpectedVersion, ct));

    private static Task<IResult> RejectApprovalAsync(Guid tenantId, Guid projectId, Guid itemId,
        ReviewWorkRequest request, HttpContext http, TransitionWorkItem useCase, CancellationToken ct) =>
        ExecuteTransitionAsync(http, tenantId, () => useCase.RejectApprovalAsync(
            new TenantId(tenantId), new ProjectId(projectId), new WorkItemId(itemId),
            Actor(http.User), request.Reason, request.ExpectedVersion, ct));

    private static async Task<IResult> ExecuteTransitionAsync(HttpContext http, Guid tenantId,
        Func<Task<WorkItemChange>> execute)
    {
        if (!TryActor(http.User, tenantId, out _))
            return Results.NotFound();
        try
        {
            var change = await execute();
            return Results.Ok(new
            {
                item = Map(change.Item, change.DependsOn.Select(id => id.Value)),
                transitions = change.Transitions.Select(Map).ToArray(),
            });
        }
        catch (NotFoundException) { return Results.NotFound(); }
        catch (ConflictException) { return Results.Conflict(); }
        catch (WorkItemConcurrencyException) { return Results.Conflict(); }
        catch (InvalidOperationException) { return Results.Conflict(); }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
    }

    private static async Task<IResult> ListTransitionsAsync(Guid tenantId, Guid projectId, Guid itemId,
        HttpContext http, ListWorkItemTransitions useCase, CancellationToken ct)
    {
        if (!TryActor(http.User, tenantId, out var actor))
            return Results.NotFound();
        try
        {
            var transitions = await useCase.ExecuteAsync(new TenantId(tenantId), new ProjectId(projectId),
                new WorkItemId(itemId), actor, ct);
            return Results.Ok(transitions.Select(Map).ToArray());
        }
        catch (NotFoundException) { return Results.NotFound(); }
    }

    private static bool TryActor(ClaimsPrincipal user, Guid routeTenantId, out PrincipalId actor)
    {
        actor = null!;
        var tenantClaim = user.FindFirstValue(AuthClaims.TenantId);
        var actorClaim = user.FindFirstValue(AuthClaims.PrincipalId);
        if (!Guid.TryParse(tenantClaim, out var tokenTenant) || tokenTenant != routeTenantId
            || !Guid.TryParse(actorClaim, out var actorId))
            return false;
        actor = new PrincipalId(actorId);
        return true;
    }

    private static PrincipalId Actor(ClaimsPrincipal user) =>
        new(Guid.Parse(user.FindFirstValue(AuthClaims.PrincipalId)!));

    private static MilestoneResponse Map(Milestone milestone) =>
        new(milestone.Id.Value, milestone.TenantId.Value, milestone.ProjectId.Value, milestone.Key,
            milestone.Title, milestone.DueAt, milestone.State, milestone.Version, milestone.CreatedBy.Value,
            milestone.CreatedAt, milestone.UpdatedAt);

    private static MilestoneTransitionResponse Map(MilestoneTransition transition) =>
        new(transition.Id.Value, transition.MilestoneId.Value, transition.FromState, transition.ToState,
            transition.ActorId.Value, transition.Reason, transition.Version, transition.RecordedAt);

    private static WorkItemResponse Map(WorkItem item, IEnumerable<Guid> dependencies) =>
        new(item.Id.Value, item.TenantId.Value, item.ProjectId.Value, item.Key, item.Title, item.Description,
            item.MilestoneId?.Value, item.OwnerId?.Value, item.DueAt, item.Priority, item.PlannedState,
            item.ReportedState, item.VerificationState, item.ApprovalState, item.Version,
            dependencies.ToArray(), item.CreatedAt, item.UpdatedAt);

    private static WorkItemTransitionResponse Map(WorkItemTransition transition) =>
        new(transition.Id.Value, transition.WorkItemId.Value, transition.Dimension, transition.FromState,
            transition.ToState, transition.ActorId.Value, transition.Reason, transition.RecordedAt,
            transition.Version);
}
