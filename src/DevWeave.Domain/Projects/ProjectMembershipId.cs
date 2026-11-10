namespace DevWeave.Domain.Projects;

public sealed record ProjectMembershipId(Guid Value)
{
    public static ProjectMembershipId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}