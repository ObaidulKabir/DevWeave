namespace DevWeave.Domain.Lifecycle;

public sealed record LifecycleProfileId(Guid Value)
{
    public static LifecycleProfileId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}