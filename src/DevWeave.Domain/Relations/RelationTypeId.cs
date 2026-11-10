namespace DevWeave.Domain.Relations;

public sealed record RelationTypeId(Guid Value)
{
    public static RelationTypeId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}