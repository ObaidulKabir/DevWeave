using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using Microsoft.AspNetCore.Authorization;

namespace DevWeave.Api.Security;

/// <summary>
/// Copies only the tenant claim produced by bearer authentication into the scoped database context.
/// Route and body tenant identifiers never establish database authority.
/// </summary>
public sealed class TenantContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        var endpointAllowsAnonymous =
            context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null;
        var routeTenant = context.Request.RouteValues.TryGetValue("tenantId", out var routeValue)
            ? routeValue?.ToString()
            : null;

        if (context.User.Identity?.IsAuthenticated != true)
        {
            // The agent credential exchange is anonymous by design. Its credential remains the
            // authority; the route tenant only scopes the credential lookup under PostgreSQL RLS.
            tenantContext.TenantId = endpointAllowsAnonymous &&
                                     Guid.TryParse(routeTenant, out var anonymousTenantId)
                ? anonymousTenantId
                : null;
            await next(context);
            return;
        }

        var tenantClaim = context.User.FindFirst(AuthClaims.TenantId)?.Value;
        var principalClaim = context.User.FindFirst(AuthClaims.PrincipalId)?.Value;
        var principalTypeClaim = context.User.FindFirst(AuthClaims.PrincipalType)?.Value;
        if (!Guid.TryParse(tenantClaim, out var tenantId) ||
            !Guid.TryParse(principalClaim, out _) ||
            !Enum.TryParse<PrincipalType>(principalTypeClaim, ignoreCase: false, out _))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        tenantContext.TenantId = tenantId;
        if (Guid.TryParse(routeTenant, out var routeTenantId) && routeTenantId != tenantId)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await next(context);
    }
}
