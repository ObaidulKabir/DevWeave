namespace DevWeave.Domain.Relations;

/// <summary>
/// Lifecycle of a relation type (SRS REL-002, REL-003). Only Active types may back a relationship.
/// </summary>
public enum RelationTypeStatus
{
    Active = 1,
    Deprecated = 2,
    Inactive = 3,
}