namespace DevWeave.Domain.Schema;

/// <summary>
/// Lifecycle status of a schema definition (SRS SCH-001, SCH-003). A schema starts as Draft,
/// becomes Published once it is safe to reference from a knowledge revision, and may be Deprecated.
/// Only Published schemas are "active": they can back a knowledge revision (KNW-002) and are listed
/// by discovery (KNW-008).
/// </summary>
public enum SchemaStatus
{
    Draft = 1,
    Published = 2,
    Deprecated = 3,
}