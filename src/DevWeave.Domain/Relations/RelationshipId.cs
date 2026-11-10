namespace DevWeave.Domain.Relations;

public sealed record RelationshipId(Guid Value)
{
    public static RelationshipId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}