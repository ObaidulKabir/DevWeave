namespace DevWeave.Domain.Relations;

/// <summary>
/// A platform-global relation type (SRS REL-002). A type defines the permitted endpoint knowledge
/// types, the edge direction, and the cardinality. Endpoint-type lists are optional: an empty list
/// means "any knowledge type is permitted" for that endpoint. Only Active types may back a concrete
/// <see cref="Relationship"/> (BR-007).
/// </summary>
public sealed class RelationType
{
    public RelationTypeId Id { get; }
    public string Code { get; }
    public string Name { get; }
    public IReadOnlyCollection<string> AllowedSourceTypeIds { get; }
    public IReadOnlyCollection<string> AllowedTargetTypeIds { get; }
    public RelationDirection Direction { get; }
    public RelationCardinality Cardinality { get; }
    public RelationTypeStatus Status { get; private set; }

    private RelationType(
        RelationTypeId id,
        string code,
        string name,
        IReadOnlyCollection<string> allowedSourceTypeIds,
        IReadOnlyCollection<string> allowedTargetTypeIds,
        RelationDirection direction,
        RelationCardinality cardinality,
        RelationTypeStatus status)
    {
        Id = id;
        Code = code;
        Name = name;
        AllowedSourceTypeIds = allowedSourceTypeIds;
        AllowedTargetTypeIds = allowedTargetTypeIds;
        Direction = direction;
        Cardinality = cardinality;
        Status = status;
    }

    public static RelationType Create(
        string code,
        string name,
        IReadOnlyCollection<string>? allowedSourceTypeIds = null,
        IReadOnlyCollection<string>? allowedTargetTypeIds = null,
        RelationDirection direction = RelationDirection.Forward,
        RelationCardinality cardinality = RelationCardinality.ManyToMany)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Relation type code is required.", nameof(code));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Relation type name is required.", nameof(name));

        return new RelationType(
            RelationTypeId.New(),
            code,
            name,
            Normalize(allowedSourceTypeIds),
            Normalize(allowedTargetTypeIds),
            direction,
            cardinality,
            RelationTypeStatus.Active);
    }

    /// <summary>
    /// Whether this type permits an edge between the two endpoint knowledge types (REL-002, SCH-007).
    /// An empty endpoint list is a wildcard for that side.
    /// </summary>
    public bool Accepts(string sourceTypeId, string targetTypeId)
    {
        if (AllowedSourceTypeIds.Count > 0 && !AllowedSourceTypeIds.Contains(sourceTypeId))
            return false;

        if (AllowedTargetTypeIds.Count > 0 && !AllowedTargetTypeIds.Contains(targetTypeId))
            return false;

        return true;
    }

    public void Deprecate()
    {
        if (Status == RelationTypeStatus.Deprecated)
            throw new InvalidOperationException("Relation type is already deprecated.");

        if (Status == RelationTypeStatus.Inactive)
            throw new InvalidOperationException("An inactive relation type cannot be deprecated.");

        Status = RelationTypeStatus.Deprecated;
    }

    public bool IsActive => Status == RelationTypeStatus.Active;

    private static IReadOnlyCollection<string> Normalize(IReadOnlyCollection<string>? values) =>
        values is null
            ? Array.Empty<string>()
            : values.Distinct(StringComparer.Ordinal).ToArray();
}