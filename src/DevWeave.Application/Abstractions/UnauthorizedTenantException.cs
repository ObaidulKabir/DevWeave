namespace DevWeave.Application.Abstractions;

/// <summary>
/// Raised when an authenticated caller attempts an access-mediated action (for example creating a
/// presigned download URL) on a resource outside their tenant scope (SRS ART-004). The API maps this to
/// HTTP 403, distinguishing an explicit authorization failure from the deny-by-default metadata reads
/// which surface <see cref="NotFoundException"/> (HTTP 404).
/// </summary>
public sealed class UnauthorizedTenantException : Exception
{
    public UnauthorizedTenantException(string message)
        : base(message)
    {
    }
}