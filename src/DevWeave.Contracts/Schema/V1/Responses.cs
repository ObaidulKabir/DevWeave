using DevWeave.Domain.Schema;

namespace DevWeave.Contracts.Schema.V1;

public sealed record SchemaResponse(
    Guid Id,
    string Namespace,
    string Version,
    string Owner,
    string Description,
    SchemaStatus Status,
    string Document,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt);

/// <summary>
/// Discovery projection returned by <c>GET /schemas/discover</c> (SRS KNW-008): only the identity of
/// an active schema, not its document.
/// </summary>
public sealed record SchemaDiscoveryItemResponse(string Namespace, string Version);

/// <summary>
/// A single path-specific validation failure (SRS KNW-004).
/// </summary>
public sealed record SchemaValidationError(string Path, string Message);

/// <summary>
/// HTTP 422 body shape for a payload validation failure: <c>{ errors: [{ path, message }] }</c>.
/// </summary>
public sealed record SchemaValidationErrorResponse(IReadOnlyList<SchemaValidationError> Errors);

/// <summary>
/// A conflict-friendly error body returned with HTTP 409 when a duplicate (Namespace, Version) is
/// registered (SRS SCH-001).
/// </summary>
public sealed record SchemaConflict(string Code, string Message);