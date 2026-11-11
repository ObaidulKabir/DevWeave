using System.Text.Json;
using DevWeave.Api.Security;
using DevWeave.Application.Abstractions;
using DevWeave.Application.Specifications;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Specifications;

namespace DevWeave.Api.Endpoints;

/// <summary>
/// Machine-readable specification decisions. Topics are predefined data; decisions are typed
/// knowledge items. Resolution returns exactly one binding decision per topic (or none).
/// </summary>
public static class SpecificationEndpoints
{
    public sealed record ProposeSpecDecisionRequest(string TopicId, string Level, JsonElement Decision, string? Reason);

    public static IEndpointRouteBuilder MapSpecificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/tenants/{tenantId:guid}")
            .RequireAuthorization(ApiAuthorizationPolicies.Human);

        group.MapGet("/spec/topics", (SpecificationService service) =>
            Results.Ok(service.Topics.Select(SpecProjection.Topic)));
        group.MapGet("/projects/{projectId:guid}/spec/effective", EffectiveAsync);
        group.MapGet("/projects/{projectId:guid}/spec/topics/{topicId}/resolve", ResolveAsync);
        group.MapPost("/projects/{projectId:guid}/spec/decisions", ProposeAsync);
        return endpoints;
    }

    private static Task<IResult> EffectiveAsync(
        Guid tenantId, Guid projectId, HttpContext http, SpecificationService service, CancellationToken ct) =>
        GuardAsync(async () => Results.Ok(SpecProjection.Effective(await service.EffectiveAsync(
            new TenantId(tenantId), new ProjectId(projectId), AuthenticatedPrincipal.GetId(http), ct))));

    private static Task<IResult> ResolveAsync(
        Guid tenantId, Guid projectId, string topicId, HttpContext http, SpecificationService service, CancellationToken ct) =>
        GuardAsync(async () => Results.Ok(SpecProjection.Resolution(await service.ResolveAsync(
            new TenantId(tenantId), new ProjectId(projectId), AuthenticatedPrincipal.GetId(http), topicId, ct))));

    private static Task<IResult> ProposeAsync(
        Guid tenantId, Guid projectId, ProposeSpecDecisionRequest request, HttpContext http,
        SpecificationService service, CancellationToken ct) =>
        GuardAsync(async () =>
        {
            if (!Enum.TryParse<SpecScopeLevel>(request.Level, ignoreCase: true, out var level))
            {
                return Results.UnprocessableEntity(new
                {
                    code = "invalid-decision",
                    message = "Level must be platform, tenant or project.",
                });
            }

            var item = await service.ProposeAsync(
                new TenantId(tenantId), new ProjectId(projectId), AuthenticatedPrincipal.GetId(http),
                request.TopicId, level, request.Decision.GetRawText(), request.Reason ?? string.Empty, ct);
            return Results.Created(
                $"/tenants/{tenantId}/knowledge/{item.Id}",
                new { itemId = item.Id.Value, item.Key, state = item.State.ToString() });
        });

    private static async Task<IResult> GuardAsync(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (NotFoundException ex)
        {
            return Results.NotFound(new { code = "not-found", message = ex.Message });
        }
        catch (UnauthorizedTenantException ex)
        {
            return Results.Json(new { code = "forbidden", message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (ConflictException ex)
        {
            return Results.Conflict(new { code = "conflict", message = ex.Message });
        }
        catch (SpecDecisionRejectedException ex)
        {
            return Results.UnprocessableEntity(new
            {
                code = "invalid-decision",
                message = ex.Message,
                issues = ex.Issues.Select(i => new { i.Path, i.Message }),
            });
        }
        catch (PayloadValidationException ex)
        {
            return Results.UnprocessableEntity(new { code = "invalid-decision", message = ex.Message });
        }
        catch (ProhibitedContentException ex)
        {
            return Results.UnprocessableEntity(new { code = "prohibited-content", message = ex.Message, reasons = ex.Reasons });
        }
    }
}
