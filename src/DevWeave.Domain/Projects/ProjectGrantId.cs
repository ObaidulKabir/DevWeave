namespace DevWeave.Domain.Projects;

public sealed record ProjectGrantId(Guid Value)
{
    public static ProjectGrantId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}