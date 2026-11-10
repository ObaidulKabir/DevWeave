namespace DevWeave.Domain.Lifecycle;

public sealed record MilestoneId(Guid Value)
{
    public static MilestoneId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}
