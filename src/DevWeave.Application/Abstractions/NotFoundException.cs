namespace DevWeave.Application.Abstractions;

/// <summary>
/// Raised when an entity cannot be found inside the caller's tenant scope. Cross-tenant enquiries
/// and genuinely missing entities surface identically so existence is never disclosed (deny-by-default).
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message)
        : base(message)
    {
    }
}