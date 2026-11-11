using DevWeave.Api.Security;
using DevWeave.Application.Abstractions;
using DevWeave.Application.Planning;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace DevWeave.Api.Endpoints;

public static class PlanDesignEndpoints
{
    public static IEndpointRouteBuilder MapPlanDesignEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/tenants/{tenantId:guid}/projects/{projectId:guid}/plan")
            .RequireAuthorization(ApiAuthorizationPolicies.Human);
        group.MapGet("/", ReadAsync);
        group.MapPost("/outline", AttachAsync);
        group.MapPost("/work-links", LinkAsync);
        group.MapPost("/proposals", ProposeAsync);
        group.MapPost("/proposals/{proposalId:guid}/decide", DecideAsync);
        return endpoints;
    }

    private static async Task<IResult> ReadAsync(Guid tenantId, Guid projectId, HttpContext http,
        PlanDesignService service, CancellationToken ct) =>
        await ExecuteAsync(() => service.ReadAsync(new(tenantId), new(projectId),
            AuthenticatedPrincipal.GetId(http), ct), view => Results.Ok(PlanDesignProjection.Outline(view)));

    private static async Task<IResult> AttachAsync(Guid tenantId, Guid projectId, AttachOutlineRequest request,
        HttpContext http, PlanDesignService service, CancellationToken ct) =>
        await ExecuteAsync(() => service.AttachAsync(new(tenantId), new(projectId),
            AuthenticatedPrincipal.GetId(http), new(request.ItemId),
            request.ParentItemId is { } parent ? new(parent) : null, request.Position, ct),
            node => Results.Created($"/tenants/{tenantId}/projects/{projectId}/plan/outline", PlanDesignProjection.Node(node)));

    private static async Task<IResult> LinkAsync(Guid tenantId, Guid projectId, CreatePlanWorkLinkRequest request,
        HttpContext http, PlanDesignService service, CancellationToken ct) =>
        await ExecuteAsync(() => service.LinkAsync(new(tenantId), new(projectId),
            AuthenticatedPrincipal.GetId(http), new(request.WorkItemId), new(request.KnowledgeItemId),
            new(request.RevisionId), ct),
            link => Results.Created($"/tenants/{tenantId}/projects/{projectId}/plan/work-links", PlanDesignProjection.Link(link)));

    private static async Task<IResult> ProposeAsync(Guid tenantId, Guid projectId, ProposeExpansionRequest request,
        HttpContext http, PlanDesignService service, CancellationToken ct) =>
        await ExecuteAsync(() => service.ProposeAsync(new(tenantId), new(projectId),
            AuthenticatedPrincipal.GetId(http), new(request.ParentItemId), request.Key,
            request.Title, request.TypeId, request.Rationale, ct),
            proposal => Results.Created($"/tenants/{tenantId}/projects/{projectId}/plan/proposals/{proposal.Id.Value}", PlanDesignProjection.Proposal(proposal)));

    private static async Task<IResult> DecideAsync(Guid tenantId, Guid projectId, Guid proposalId,
        DecideExpansionRequest request, HttpContext http, PlanDesignService service, CancellationToken ct) =>
        await ExecuteAsync(() => service.DecideAsync(new(tenantId), new(projectId),
            AuthenticatedPrincipal.GetId(http), new(proposalId),
            request.AcceptedItemId is { } id ? new(id) : null, ct), proposal => Results.Ok(PlanDesignProjection.Proposal(proposal)));

    private static async Task<IResult> ExecuteAsync<T>(Func<Task<T>> action, Func<T, IResult> success)
    {
        try { return success(await action()); }
        catch (NotFoundException) { return Results.NotFound(); }
        catch (UnauthorizedTenantException) { return Results.NotFound(); }
        catch (ConflictException ex) { return Results.Conflict(new { error = ex.Message }); }
        catch (DbUpdateConcurrencyException) { return Results.Conflict(new { error = "Planning record changed during review." }); }
        catch (DbUpdateException) { return Results.Conflict(new { error = "Planning record conflicts with existing project data." }); }
        catch (ProhibitedContentException) { return Results.UnprocessableEntity(new { error = "Proposal rejected by content policy." }); }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
    }
}

public sealed record AttachOutlineRequest(Guid ItemId, Guid? ParentItemId, int Position);
public sealed record CreatePlanWorkLinkRequest(Guid WorkItemId, Guid KnowledgeItemId, Guid RevisionId);
public sealed record ProposeExpansionRequest(Guid ParentItemId, string Key, string Title, string TypeId, string Rationale);
public sealed record DecideExpansionRequest(Guid? AcceptedItemId);
