namespace DevWeave.Domain.Lifecycle;

public sealed record WorkItemDependencyId(Guid Value)
{
    public static WorkItemDependencyId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}
