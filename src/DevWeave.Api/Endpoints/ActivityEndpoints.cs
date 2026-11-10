using DevWeave.Application.Abstractions;
using DevWeave.Application.Activities;
using DevWeave.Contracts.Activities.V1;
using DevWeave.Domain.Activities;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Api.Endpoints;

/// <summary>
/// Provenance REST surface for Slice 6 (SRS PRO-001..PRO-005, PRO-008, REV-006, REV-007, REV-008).
/// Activities are append-only: there is no update or delete endpoint, only immutable record + reads.
/// Reads and write are tenant-scoped (deny-by-default); the tenant identifier is passed in the route.
/// The REV-008 amendment guard surfaces as HTTP 409.
/// </summary>
public static class ActivityEndpoints
{
    public static IEndpointRouteBuilder MapActivityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/tenants/{tenantId:guid}");

        group.MapPost("/projects/{projectId:guid}/activities", RecordActivityAsync);
        group.MapGet("/projects/{projectId:guid}/activities", ListActivitiesForProjectAsync);
        group.MapGet("/projects/{projectId:guid}/state", ReconstructProjectStateAsync);
        group.MapGet("/projects/{projectId:guid}/provenance", ExportProvenanceAsync);
        group.MapGet("/activities/{activityId:guid}", GetActivityAsync);
        group.MapGet("/knowledge-items/{itemId:guid}/activities", ListActivitiesForItemAsync);
        group.MapGet("/principals/{actorId:guid}/activities", ListActivitiesByActorAsync);

        return endpoints;
    }

    private static async Task<IResult> RecordActivityAsync(
        Guid tenantId,
        Guid projectId,
        RecordActivityRequest request,
        RecordActivity useCase,
        CancellationToken ct)
    {
        try
        {
            var activity = await useCase.ExecuteAsync(
                new TenantId(tenantId),
                new ProjectId(projectId),
                request.Type,
                new PrincipalId(request.Actor),
                request.Purpose,
                request.Status,
                request.StartedAt,
                request.EndedAt,
                request.CorrelationId,
                request.Reason,
                request.Reference is { } reference ? new KnowledgeRevisionId(reference) : null,
                (request.Inputs ?? Array.Empty<ActivityInputRequest>())
                    .Select(i => new ActivityInput(new KnowledgeItemId(i.ItemId), new KnowledgeRevisionId(i.RevisionId), i.Position))
                    .ToList(),
                (request.Outputs ?? Array.Empty<ActivityOutputRequest>())
                    .Select(o => new ActivityOutput(new KnowledgeItemId(o.ItemId), new KnowledgeRevisionId(o.RevisionId), o.Position))
                    .ToList(),
                ct);

            return Results.Created(
                $"/tenants/{tenantId}/activities/{activity.Id}",
                Map(activity));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
        catch (ConflictException ex)
        {
            return Results.Conflict(new ActivityError("amendment-requires-reference", ex.Message));
        }
    }

    private static async Task<IResult> ListActivitiesForProjectAsync(
        Guid tenantId,
        Guid projectId,
        ListActivitiesForProject useCase,
        CancellationToken ct)
    {
        var activities = await useCase.ExecuteAsync(new TenantId(tenantId), new ProjectId(projectId), ct);

        return Results.Ok(activities.Select(Map).ToList());
    }

    private static async Task<IResult> GetActivityAsync(
        Guid tenantId,
        Guid activityId,
        GetActivity useCase,
        CancellationToken ct)
    {
        var activity = await useCase.ExecuteAsync(new TenantId(tenantId), new ActivityId(activityId), ct);

        return activity is null
            ? Results.NotFound()
            : Results.Ok(Map(activity));
    }

    private static async Task<IResult> ListActivitiesForItemAsync(
        Guid tenantId,
        Guid itemId,
        ListActivitiesForItem useCase,
        CancellationToken ct)
    {
        var activities = await useCase.ExecuteAsync(new TenantId(tenantId), new KnowledgeItemId(itemId), ct);

        return Results.Ok(activities.Select(Map).ToList());
    }

    private static async Task<IResult> ListActivitiesByActorAsync(
        Guid tenantId,
        Guid actorId,
        ListActivitiesByActor useCase,
        CancellationToken ct)
    {
        var activities = await useCase.ExecuteAsync(new TenantId(tenantId), new PrincipalId(actorId), ct);

        return Results.Ok(activities.Select(Map).ToList());
    }

    private static async Task<IResult> ReconstructProjectStateAsync(
        Guid tenantId,
        Guid projectId,
        DateTimeOffset? at,
        ReconstructProjectState useCase,
        CancellationToken ct)
    {
        var result = await useCase.ExecuteAsync(new TenantId(tenantId), new ProjectId(projectId), at, ct);

        return Results.Ok(new ProjectStateReconstructionResponse(
            result.Activities.Select(Map).ToList(),
            result.Items
                .Select(i => new ItemVersionSnapshotResponse(i.ItemId.Value, i.RevisionId?.Value, i.RevisionNumber))
                .ToList()));
    }

    private static async Task<IResult> ExportProvenanceAsync(
        Guid tenantId,
        Guid projectId,
        ExportProvenance useCase,
        CancellationToken ct)
    {
        var records = await useCase.ExecuteAsync(new TenantId(tenantId), new ProjectId(projectId), ct);

        return Results.Ok(records.Select(r => new ActivityProvenanceResponse(
            r.ActivityId,
            r.ActivityType,
            r.Agent,
            r.Used.Select(u => new ProvenanceRefResponse(u.ItemId, u.RevisionId)).ToList(),
            r.Generated.Select(g => new ProvenanceRefResponse(g.ItemId, g.RevisionId)).ToList())).ToList());
    }

    private static ActivityResponse Map(Activity activity) =>
        new(
            activity.Id.Value,
            activity.TenantId.Value,
            activity.ProjectId.Value,
            activity.Type,
            activity.Actor.Value,
            activity.Purpose,
            activity.Status,
            activity.StartedAt,
            activity.EndedAt,
            activity.CorrelationId,
            activity.Reason,
            activity.Reference?.Value,
            activity.Inputs
                .OrderBy(i => i.Position)
                .Select(i => new ActivityInputResponse(i.ItemId.Value, i.RevisionId.Value, i.Position))
                .ToList(),
            activity.Outputs
                .OrderBy(o => o.Position)
                .Select(o => new ActivityOutputResponse(o.ItemId.Value, o.RevisionId.Value, o.Position))
                .ToList());
}