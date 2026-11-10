using DevWeave.Api.Security;
using DevWeave.Application.Abstractions;
using DevWeave.Application.Knowledge;
using DevWeave.Contracts.Knowledge.V1;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Api.Endpoints;

/// <summary>
/// Project-scoped full-text search. Membership and classification are evaluated before database
/// ranking, pagination, or result materialization.
/// </summary>
public static class SearchEndpoints
{
    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/tenants/{tenantId:guid}/projects/{projectId:guid}/search",
                SearchKnowledgeAsync)
            .RequireAuthorization(ApiAuthorizationPolicies.Human);

        return endpoints;
    }

    private static async Task<IResult> SearchKnowledgeAsync(
        Guid tenantId,
        Guid projectId,
        SearchKnowledgeRequest request,
        HttpContext http,
        SearchKnowledge useCase,
        CancellationToken ct)
    {
        try
        {
            var results = await useCase.ExecuteAsync(
                new TenantId(tenantId),
                new ProjectId(projectId),
                AuthenticatedPrincipal.GetId(http),
                request,
                ct);

            return Results.Ok(results);
        }
        catch (UnauthorizedTenantException ex)
        {
            return Results.Json(
                new { code = "forbidden", message = ex.Message },
                statusCode: StatusCodes.Status403Forbidden);
        }
        catch (ProhibitedContentException ex)
        {
            return Results.UnprocessableEntity(new
            {
                code = "prohibited-content",
                message = ex.Message,
                reasons = ex.Reasons,
            });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { code = "invalid-search", message = ex.Message });
        }
    }
}
