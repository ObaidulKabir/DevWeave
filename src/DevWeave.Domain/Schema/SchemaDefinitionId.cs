namespace DevWeave.Domain.Schema;

public sealed record SchemaDefinitionId(Guid Value)
{
    public static SchemaDefinitionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}