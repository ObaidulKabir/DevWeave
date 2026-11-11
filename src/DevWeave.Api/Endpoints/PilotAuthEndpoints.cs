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
        [FromBody] PilotBootstrapRequest request,
        CreateTenant createTenant,
        CreatePrincipal createPrincipal,
        InviteMember inviteMember,
        ActivateMember activateMember,
        PilotHumanTokenService tokens,
        IHostEnvironment environment,
        IConfiguration configuration,
        CancellationToken ct)
    {
        if (!IsPilotEnabled(environment, configuration))
            return Results.NotFound();

        if (string.IsNullOrWhiteSpace(request.TenantName) || string.IsNullOrWhiteSpace(request.OwnerEmail))
            return Results.BadRequest(new { error = "tenantName and ownerEmail are required." });

        var tenant = await createTenant.ExecuteAsync(request.TenantName.Trim(), ct);
        var owner = await createPrincipal.ExecuteAsync(
            tenant.Id, request.OwnerEmail.Trim(), PrincipalType.Human, ct);
        var membership = await inviteMember.ExecuteAsync(
            tenant.Id, owner.Id, MembershipRole.Owner, ct);
        await activateMember.ExecuteAsync(tenant.Id, membership.Id, ct);

        var accessToken = tokens.Issue(tenant.Id, owner.Id, platformAdmin: true);

        return Results.Ok(new
        {
            tenantId = tenant.Id.Value,
            principalId = owner.Id.Value,
            email = owner.Email,
            accessToken,
            tokenType = "Bearer",
            expiresInSeconds = 8 * 60 * 60,
            note = "Development pilot token. Do not use in production.",
        });
    }

    private static async Task<IResult> MintTokenAsync(
        [FromBody] PilotTokenRequest request,
        IPrincipalRepository principals,
        PilotHumanTokenService tokens,
        IHostEnvironment environment,
        IConfiguration configuration,
        CancellationToken ct)
    {
        if (!IsPilotEnabled(environment, configuration))
            return Results.NotFound();

        var principal = await principals.GetByIdAsync(
            new TenantId(request.TenantId), new PrincipalId(request.PrincipalId), ct);
        if (principal is null || !principal.IsActive || principal.Type != PrincipalType.Human)
            return Results.NotFound();

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
