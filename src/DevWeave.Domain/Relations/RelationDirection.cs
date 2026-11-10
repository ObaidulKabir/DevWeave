namespace DevWeave.Domain.Relations;

/// <summary>
/// Whether a relation type describes a single-direction edge or a symmetric edge (SRS REL-002).
/// Cardinality and endpoint validation are enforced against the stored (source, target) orientation
/// regardless of this value; direction is metadata consumed when interpreting or traversing edges.
/// </summary>
public enum RelationDirection
{
    Forward = 1,
    Bidirectional = 2,
}