namespace DevWeave.Domain.Relations;

/// <summary>
/// Query direction for relationship traversal relative to the start item (SRS REL-004). Outgoing walks
/// the stored source-to-target orientation; Incoming walks it in reverse.
/// </summary>
public enum TraversalDirection
{
    Outgoing = 1,
    Incoming = 2,
}