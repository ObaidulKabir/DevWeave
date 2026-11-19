using DevWeave.Api.Security;
using DevWeave.Application.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DevWeave.Api.Endpoints;

/// <summary>
/// Anonymous tenant registration. Creates the tenant and an active Owner principal immediately.
/// A development session token is included only while pilot auth is enabled. No verification mail is sent.
/// </summary>
public static class RegistrationEndpoints
{
    public static IEndpointRouteBuilder MapRegistrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/auth/register", RegisterAsync).AllowAnonymous();
        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(
        [FromBody] RegisterTenantRequest? request,
        RegisterTenant registerTenant,
        IServiceProvider services,
        IHostEnvironment environment,
        IConfiguration configuration,
        CancellationToken ct)
    {
        if (request is null)
            return Results.BadRequest(new { error = "tenantName and ownerEmail are required." });

        try
        {
            var registered = await registerTenant.ExecuteAsync(request.TenantName, request.OwnerEmail, ct);
            string? accessToken = null;
            if (PilotAuthEndpoints.IsPilotEnabled(environment, configuration))
            {
                accessToken = services.GetRequiredService<PilotHumanTokenService>()
                    .Issue(registered.Tenant.Id, registered.Owner.Id, platformAdmin: true);
            }

            return Results.Ok(new
            {
                tenantId = registered.Tenant.Id.Value,
                principalId = registered.Owner.Id.Value,
                email = registered.Owner.Email,
                accessToken,
                tokenType = accessToken is null ? null : "Bearer",
                expiresInSeconds = accessToken is null ? (int?)null : 8 * 60 * 60,
            });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    public sealed record RegisterTenantRequest(string TenantName, string OwnerEmail);
}
