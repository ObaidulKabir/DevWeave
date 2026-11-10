namespace DevWeave.Domain.Activities;

public sealed record ActivityId(Guid Value)
{
    public static ActivityId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}