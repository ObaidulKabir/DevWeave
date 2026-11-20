using DevWeave.Api.Security;
using DevWeave.Application.Abstractions;
using DevWeave.Application.Identity;
using DevWeave.Domain.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DevWeave.Api.Endpoints;

/// <summary>Time-bounded platform support access grants (SRS TEN-009).</summary>
public static class SupportAccessEndpoints
{
    public static IEndpointRouteBuilder MapSupportAccessEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Tenant Owner/Admin manages grants (membership middleware). Operators then use an active
        // grant with a platform_admin claim when they lack ordinary membership (TEN-009).
        var group = endpoints.MapGroup("/tenants/{tenantId:guid}/support-access")
            .RequireAuthorization(ApiAuthorizationPolicies.Human);

        group.MapGet("", ListAsync);
        group.MapPost("", IssueAsync);
        group.MapPost("/{grantId:guid}/revoke", RevokeAsync);

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        Guid tenantId,
        ManageSupportAccess useCase,
        CancellationToken ct)
    {
        var grants = await useCase.ListAsync(new TenantId(tenantId), ct);
        return Results.Ok(grants.Select(Map));
    }

    private static async Task<IResult> IssueAsync(
        Guid tenantId,
        [FromBody] IssueSupportAccessRequest? request,
        ManageSupportAccess useCase,
        HttpContext http,
        CancellationToken ct)
    {
        if (request is null)
            return Results.BadRequest(new { error = "operatorId, purpose, and lifetimeHours are required." });

        if (!Guid.TryParse(http.User.FindFirst(AuthClaims.PrincipalId)?.Value, out var approverId))
            return Results.Unauthorized();

        try
        {
            var lifetime = TimeSpan.FromHours(request.LifetimeHours <= 0 ? 8 : request.LifetimeHours);
            var grant = await useCase.IssueAsync(
                new TenantId(tenantId),
                new PrincipalId(request.OperatorId),
                new PrincipalId(approverId),
                request.Purpose,
                lifetime,
                ct);

            return Results.Created(
                $"/tenants/{tenantId}/support-access/{grant.Id.Value}",
                Map(grant));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
        catch (ConflictException ex)
        {
            return Results.Conflict(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> RevokeAsync(
        Guid tenantId,
        Guid grantId,
        ManageSupportAccess useCase,
        HttpContext http,
        CancellationToken ct)
    {
        if (!Guid.TryParse(http.User.FindFirst(AuthClaims.PrincipalId)?.Value, out var actorId))
            return Results.Unauthorized();

        try
        {
            await useCase.RevokeAsync(
                new TenantId(tenantId),
                new SupportAccessGrantId(grantId),
                new PrincipalId(actorId),
                ct);
            return Results.NoContent();
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { error = ex.Message });
        }
    }

    private static object Map(SupportAccessGrant grant) => new
    {
        id = grant.Id.Value,
        tenantId = grant.TenantId.Value,
        operatorId = grant.OperatorId.Value,
        approverId = grant.ApproverId.Value,
        purpose = grant.Purpose,
        createdAt = grant.CreatedAt,
        expiresAt = grant.ExpiresAt,
        revokedAt = grant.RevokedAt,
        active = grant.IsActive(DateTimeOffset.UtcNow),
    };

    public sealed record IssueSupportAccessRequest(Guid OperatorId, string Purpose, int LifetimeHours = 8);
}
