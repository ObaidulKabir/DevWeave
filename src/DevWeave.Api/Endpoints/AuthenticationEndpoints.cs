using DevWeave.Api.Security;
using DevWeave.Application.Abstractions;
using DevWeave.Application.Agents;
using DevWeave.Contracts.Agents.V1;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;

namespace DevWeave.Api.Endpoints;

/// <summary>
/// Credential-to-token exchange and verified-identity surface (SRS TEN-007). Agents authenticate with a
/// credential secret and receive a short-lived bearer token; the <c>/auth/whoami</c> endpoint proves the
/// middleware-derived identity is available to authorization-aware handlers.
/// </summary>
public static class AuthenticationEndpoints
{
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromHours(1);

    public static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/tenants/{tenantId:guid}/agents/{agentId:guid}/authenticate", AuthenticateAgentAsync)
            .AllowAnonymous();

        endpoints.MapGet("/auth/whoami", WhoAmIAsync).RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> AuthenticateAgentAsync(
        Guid tenantId,
        Guid agentId,
        AuthenticateAgentRequest request,
        AuthenticateAgent useCase,
        IAccessTokenService tokens,
        CancellationToken ct)
    {
        try
        {
            var agent = await useCase.ExecuteAsync(
                new TenantId(tenantId),
                new PrincipalId(agentId),
                new AgentCredentialId(request.CredentialId),
                request.Secret,
                ct);

            var accessToken = tokens.Issue(agent.TenantId, agent.Id, agent.Type, AccessTokenLifetime);

            return Results.Ok(new AccessTokenResponse(
                accessToken,
                "Bearer",
                (int)AccessTokenLifetime.TotalSeconds));
        }
        catch (UnauthorizedTenantException)
        {
            return Results.Json(
                new AgentError("authentication-failed", "Invalid agent credentials."),
                statusCode: StatusCodes.Status401Unauthorized);
        }
    }

    private static IResult WhoAmIAsync(HttpContext context)
    {
        var user = context.User;

        return Results.Ok(new
        {
            tenantId = user.FindFirst(AuthClaims.TenantId)?.Value,
            principalId = user.FindFirst(AuthClaims.PrincipalId)?.Value,
            principalType = user.FindFirst(AuthClaims.PrincipalType)?.Value,
        });
    }
}
