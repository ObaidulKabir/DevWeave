using System.Security.Claims;
using System.Text.Encodings.Web;
using DevWeave.Application.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace DevWeave.Api.Security;

/// <summary>
/// Authenticates the request from a <c>Bearer</c> authorization header using the stateless token service
/// (SRS TEN-007). A valid token yields a <see cref="ClaimsPrincipal"/> carrying the verified tenant,
/// principal and principal type, which authorization policies can then enforce. No token or an invalid
/// token produces a failed authentication result that downstream authorization treats as unauthenticated.
/// </summary>
public sealed class BearerTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Bearer";

    private readonly IAccessTokenService _tokens;

    public BearerTokenAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IAccessTokenService tokens)
        : base(options, logger, encoder)
    {
        _tokens = tokens;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());

        var token = header["Bearer ".Length..].Trim();
        var subject = _tokens.Validate(token);
        if (subject is null)
            return Task.FromResult(AuthenticateResult.Fail("Invalid or expired access token."));

        var claims = new[]
        {
            new Claim(AuthClaims.TenantId, subject.TenantId.Value.ToString()),
            new Claim(AuthClaims.PrincipalId, subject.PrincipalId.Value.ToString()),
            new Claim(AuthClaims.PrincipalType, subject.PrincipalType.ToString()),
        };

        var identity = new ClaimsIdentity(claims, SchemeName, AuthClaims.PrincipalId, roleType: null);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}