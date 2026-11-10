namespace DevWeave.Domain.Lifecycle;

public sealed record WorkItemId(Guid Value)
{
    public static WorkItemId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}
