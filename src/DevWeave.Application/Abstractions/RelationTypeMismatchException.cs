namespace DevWeave.Application.Abstractions;

/// <summary>
/// Raised when a relationship violates a relation-type rule that is not a missing entity: an unknown
/// or inactive relation type (BR-007), an endpoint-type mismatch (REL-002/SCH-007), or a cardinality
/// violation (SCH-007). Carries a stable machine-readable <see cref="Code"/> so the API can map the
/// violation to a precise error body.
/// </summary>
public sealed class RelationTypeMismatchException : Exception
{
    public RelationTypeMismatchException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}