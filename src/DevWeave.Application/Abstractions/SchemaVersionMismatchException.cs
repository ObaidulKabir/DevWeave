namespace DevWeave.Application.Abstractions;

/// <summary>
/// Raised when a revision's caller-supplied schema version does not match the schema version pinned
/// on its knowledge item (SRS REV-002 / SRS 7.3). The item's pinned version is authoritative, so the
/// two can never be allowed to diverge. The API layer maps this to HTTP 409 Conflict with a distinct
/// <c>schema-version-mismatch</c> code so it is never confused with a stale-revision conflict.
/// </summary>
public sealed class SchemaVersionMismatchException : Exception
{
    public SchemaVersionMismatchException(string message)
        : base(message)
    {
    }
}
