namespace DevWeave.Domain.Agents;

public sealed record AgentGrantId(Guid Value)
{
    public static AgentGrantId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}