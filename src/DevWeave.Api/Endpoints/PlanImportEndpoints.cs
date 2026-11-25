using DevWeave.Api.Security;
using DevWeave.Application.Abstractions;
using DevWeave.Application.Planning;
using DevWeave.Contracts.Planning.V1;
using DevWeave.Domain.Planning;
using Microsoft.EntityFrameworkCore;

namespace DevWeave.Api.Endpoints;

public static class PlanImportEndpoints
{
    public static IEndpointRouteBuilder MapPlanImportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/tenants/{tenantId:guid}/projects/{projectId:guid}/plan-import-proposals")
            .RequireAuthorization(ApiAuthorizationPolicies.Human);
        group.MapGet("/", ListAsync);
        group.MapGet("/{proposalId:guid}", GetAsync);
        group.MapPost("/{proposalId:guid}/approve", ApproveAsync);
        group.MapPost("/{proposalId:guid}/reject", RejectAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        Guid tenantId, Guid projectId, HttpContext http, PlanImportService service, CancellationToken ct) =>
        await ExecuteAsync(
            async () =>
            {
                var rows = await service.ListAsync(new(tenantId), new(projectId), AuthenticatedPrincipal.GetId(http), ct);
                return rows.Select(service.ToSummary).ToArray();
            },
            Results.Ok);

    private static async Task<IResult> GetAsync(
        Guid tenantId, Guid projectId, Guid proposalId, HttpContext http, PlanImportService service, CancellationToken ct) =>
        await ExecuteAsync(
            async () =>
            {
                var proposal = await service.GetAsync(new(tenantId), new(projectId), AuthenticatedPrincipal.GetId(http), new(proposalId), ct);
                return service.ToDetail(proposal);
            },
            Results.Ok);

    private static async Task<IResult> ApproveAsync(
        Guid tenantId, Guid projectId, Guid proposalId, DecideInitialPlanProposalRequest? request,
        HttpContext http, PlanImportService service, CancellationToken ct) =>
        await ExecuteAsync(
            async () =>
            {
                var (proposal, _) = await service.ApproveAsync(
                    new(tenantId), new(projectId), AuthenticatedPrincipal.GetId(http), new(proposalId), request?.Note, ct);
                return service.ToDetail(proposal);
            },
            Results.Ok);

    private static async Task<IResult> RejectAsync(
        Guid tenantId, Guid projectId, Guid proposalId, DecideInitialPlanProposalRequest? request,
        HttpContext http, PlanImportService service, CancellationToken ct) =>
        await ExecuteAsync(
            async () =>
            {
                var proposal = await service.RejectAsync(
                    new(tenantId), new(projectId), AuthenticatedPrincipal.GetId(http), new(proposalId), request?.Note, ct);
                return service.ToDetail(proposal);
            },
            Results.Ok);

    private static async Task<IResult> ExecuteAsync<T>(Func<Task<T>> action, Func<T, IResult> success)
    {
        try { return success(await action()); }
        catch (NotFoundException) { return Results.NotFound(); }
        catch (UnauthorizedTenantException) { return Results.NotFound(); }
        catch (ConflictException ex) { return Results.Conflict(new { error = ex.Message }); }
        catch (DbUpdateConcurrencyException) { return Results.Conflict(new { error = "Plan import proposal changed during review." }); }
        catch (DbUpdateException) { return Results.Conflict(new { error = "Plan import conflicts with existing project data." }); }
        catch (ProhibitedContentException) { return Results.UnprocessableEntity(new { error = "Plan import rejected by content policy." }); }
        catch (PayloadValidationException ex) { return Results.UnprocessableEntity(new { error = ex.Message }); }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
    }
}
