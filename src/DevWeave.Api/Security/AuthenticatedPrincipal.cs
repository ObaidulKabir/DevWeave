using DevWeave.Domain.Identity;

namespace DevWeave.Api.Security;

internal static class AuthenticatedPrincipal
{
    public static PrincipalId GetId(HttpContext context) =>
        Guid.TryParse(context.User.FindFirst(AuthClaims.PrincipalId)?.Value, out var principalId)
            ? new PrincipalId(principalId)
            : throw new InvalidOperationException("The authenticated principal claim is missing or invalid.");
}
