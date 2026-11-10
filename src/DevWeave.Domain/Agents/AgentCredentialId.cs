namespace DevWeave.Domain.Agents;

public sealed record AgentCredentialId(Guid Value)
{
    public static AgentCredentialId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}