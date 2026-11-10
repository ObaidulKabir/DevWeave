namespace DevWeave.Domain.Identity;

public sealed record PrincipalId(Guid Value)
{
    public static PrincipalId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
