using System.Security.Claims;
using System.Text.Encodings.Web;
using DevWeave.Api.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevWeave.Api.IntegrationTests;

public sealed class TestTenantAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestTenant";
    private static readonly Guid TestPrincipalId = Guid.Parse("75f2ab39-d852-4a79-8a3c-b167dd2a1da2");

    public TestTenantAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var tenantId = Request.RouteValues.TryGetValue("tenantId", out var routeValue) &&
                       Guid.TryParse(routeValue?.ToString(), out var routeTenantId)
            ? routeTenantId
            : Guid.Empty;
        var claims = new[]
        {
            new Claim(AuthClaims.TenantId, tenantId.ToString("D")),
            new Claim(AuthClaims.PrincipalId, TestPrincipalId.ToString("D")),
            new Claim(AuthClaims.PrincipalType, "Human"),
            new Claim("platform_admin", "true"),
        };
        var identity = new ClaimsIdentity(claims, SchemeName, AuthClaims.PrincipalId, roleType: null);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
