namespace DevWeave.Domain.Lifecycle;

public sealed record WorkItemTransitionId(Guid Value)
{
    public static WorkItemTransitionId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}
