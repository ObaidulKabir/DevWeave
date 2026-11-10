namespace DevWeave.Domain.Projects;

public sealed record ProjectId(Guid Value)
{
    public static ProjectId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}