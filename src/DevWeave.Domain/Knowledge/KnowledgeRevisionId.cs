namespace DevWeave.Domain.Knowledge;

public sealed record KnowledgeRevisionId(Guid Value)
{
    public static KnowledgeRevisionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}