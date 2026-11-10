namespace DevWeave.Application.Abstractions;

/// <summary>
/// Raised when a relationship would span two different projects within a tenant (SRS REL-007). Unlike
/// cross-tenant references (which are indistinguishable from missing entities and surface as
/// <see cref="NotFoundException"/>), cross-project references are detectable within the tenant and
/// surface as a distinct 409 conflict. No grant mechanism exists in this slice.
/// </summary>
public sealed class CrossProjectReferenceException : Exception
{
    public const string Code = "cross-project-reference";

    public CrossProjectReferenceException(string message)
        : base(message)
    {
    }
}