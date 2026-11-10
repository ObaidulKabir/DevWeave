using DevWeave.Application.Abstractions;

namespace DevWeave.Api.Security;

/// <summary>
/// Copies only the tenant claim produced by bearer authentication into the scoped database context.
/// Route and body tenant identifiers never establish database authority.
/// </summary>
public sealed class TenantContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        var claim = context.User.FindFirst(AuthClaims.TenantId)?.Value;
        tenantContext.TenantId = Guid.TryParse(claim, out var tenantId) ? tenantId : null;

        await next(context);
    }
}
