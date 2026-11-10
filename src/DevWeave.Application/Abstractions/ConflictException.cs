namespace DevWeave.Application.Abstractions;

/// <summary>
/// Raised when a cross-entity invariant is violated (for example a duplicate project key within a
/// tenant). The API layer maps this to HTTP 409 Conflict.
/// </summary>
public sealed class ConflictException : Exception
{
    public ConflictException(string message)
        : base(message)
    {
    }
}