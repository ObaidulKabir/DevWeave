namespace DevWeave.Domain.Identity;

public sealed record MembershipId(Guid Value)
{
    public static MembershipId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}