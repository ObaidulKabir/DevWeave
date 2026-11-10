namespace DevWeave.Domain.Schema;

/// <summary>
/// A single path-specific validation failure (SRS KNW-004). The path is a JSON Pointer describing
/// where the invalid value sits inside the payload (for example "/age"), which lets a client or
/// agent surface exactly which field failed rather than a blanket "invalid payload" message.
/// </summary>
public sealed record SchemaValidationIssue(string Path, string Message);