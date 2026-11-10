namespace DevWeave.Domain.Knowledge;

public sealed record KnowledgeItemId(Guid Value)
{
    public static KnowledgeItemId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}