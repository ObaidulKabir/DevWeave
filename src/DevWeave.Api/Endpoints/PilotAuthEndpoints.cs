using DevWeave.Api.Security;
using DevWeave.Application.Abstractions;
using DevWeave.Application.Identity;
using DevWeave.Domain.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DevWeave.Api.Endpoints;

/// <summary>
/// Development pilot bootstrap for human JWTs when OIDC authority is unset.
/// Never registered in Production.
/// </summary>
public static class PilotAuthEndpoints
{
    public static IEndpointRouteBuilder MapPilotAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/auth/pilot").AllowAnonymous();

        group.MapPost("/bootstrap", BootstrapAsync);
        group.MapPost("/token", MintTokenAsync);

        return endpoints;
    }

    private static async Task<IResult> BootstrapAsync(
        [FromBody] PilotBootstrapRequest? request,
        RegisterTenant registerTenant,
        PilotHumanTokenService tokens,
        IHostEnvironment environment,
        IConfiguration configuration,
        CancellationToken ct)
    {
        if (!IsPilotEnabled(environment, configuration))
            return Results.NotFound(new
            {
                error = "GUID sign-in is disabled on this host. Use your identity provider, or run the API in Development without an OIDC authority.",
            });

        if (request is null)
            return Results.BadRequest(new { error = "tenantName and ownerEmail are required." });

        try
        {
            var registered = await registerTenant.ExecuteAsync(request.TenantName, request.OwnerEmail, ct);
            var accessToken = tokens.Issue(registered.Tenant.Id, registered.Owner.Id, platformAdmin: true);

            return Results.Ok(new
            {
                tenantId = registered.Tenant.Id.Value,
                principalId = registered.Owner.Id.Value,
                email = registered.Owner.Email,
                accessToken,
                tokenType = "Bearer",
                expiresInSeconds = 8 * 60 * 60,
                note = "Development pilot token. Do not use in production.",
            });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> MintTokenAsync(
        [FromBody] PilotTokenRequest request,
        IPrincipalRepository principals,
        ITenantContext tenantContext,
        PilotHumanTokenService tokens,
        IHostEnvironment environment,
        IConfiguration configuration,
        CancellationToken ct)
    {
        if (!IsPilotEnabled(environment, configuration))
            return Results.NotFound(new
            {
                error = "GUID sign-in is disabled on this host. Use your identity provider, or run the API in Development without an OIDC authority.",
            });

        // Principals are behind RLS. Anonymous /auth/pilot/token has no JWT tenant claim and no
        // {tenantId} route value, so the session tenant must come from the request before the lookup.
        tenantContext.TenantId = request.TenantId;

        var principal = await principals.GetByIdAsync(
            new TenantId(request.TenantId), new PrincipalId(request.PrincipalId), ct);
        if (principal is null || !principal.IsActive || principal.Type != PrincipalType.Human)
            return Results.NotFound(new
            {
                error = "No active Owner principal matches those tenant and owner IDs. Register a tenant first, or check both values.",
            });

        var accessToken = tokens.Issue(
            principal.TenantId, principal.Id, platformAdmin: request.PlatformAdmin);

        return Results.Ok(new
        {
            tenantId = principal.TenantId.Value,
            principalId = principal.Id.Value,
            accessToken,
            tokenType = "Bearer",
            expiresInSeconds = 8 * 60 * 60,
        });
    }

    internal static bool IsPilotEnabled(IHostEnvironment environment, IConfiguration configuration) =>
        environment.IsDevelopment() &&
        string.IsNullOrWhiteSpace(configuration["Auth:Oidc:Authority"]);

    public sealed record PilotBootstrapRequest(string TenantName, string OwnerEmail);
    public sealed record PilotTokenRequest(Guid TenantId, Guid PrincipalId, bool PlatformAdmin = true);
}
