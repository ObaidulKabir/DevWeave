namespace DevWeave.Domain.Agents;

public sealed record ContextPackageId(Guid Value)
{
    public static ContextPackageId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}