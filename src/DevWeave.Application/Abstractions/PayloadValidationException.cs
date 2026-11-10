using DevWeave.Domain.Schema;

namespace DevWeave.Application.Abstractions;

/// <summary>
/// Raised when a typed revision payload fails validation against its registered JSON Schema
/// (SRS KNW-004). Carries the ordered list of path-specific issues so the API can return a 422 body
/// of the shape <c>{ errors: [{ path, message }] }</c>.
/// </summary>
public sealed class PayloadValidationException : Exception
{
    public PayloadValidationException(IReadOnlyList<SchemaValidationIssue> issues)
        : base("The payload does not conform to the registered JSON Schema.")
    {
        Issues = issues;
    }

    public IReadOnlyList<SchemaValidationIssue> Issues { get; }
}