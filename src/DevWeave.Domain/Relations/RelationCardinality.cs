namespace DevWeave.Domain.Relations;

/// <summary>
/// How many edges of a relation type a knowledge item may participate in (SRS REL-002, SCH-007).
/// </summary>
public enum RelationCardinality
{
    ManyToMany = 1,
    OneToMany = 2,
    OneToOne = 3,
}