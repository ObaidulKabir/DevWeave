namespace DevWeave.Domain.Lifecycle;

public sealed record MilestoneTransitionId(Guid Value)
{
    public static MilestoneTransitionId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}
