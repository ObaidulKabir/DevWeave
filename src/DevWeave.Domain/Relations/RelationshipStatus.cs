namespace DevWeave.Domain.Relations;

/// <summary>
/// Lifecycle of a concrete relationship (SRS REL-001). Revoked is terminal: a revoked edge is hidden
/// from traversal but remains stored for auditability.
/// </summary>
public enum RelationshipStatus
{
    Active = 1,
    Revoked = 2,
}