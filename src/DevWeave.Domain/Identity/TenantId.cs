namespace DevWeave.Domain.Identity;

public sealed record TenantId(Guid Value)
{
    public static TenantId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
