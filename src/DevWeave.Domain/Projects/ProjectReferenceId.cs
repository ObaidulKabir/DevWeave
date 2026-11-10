namespace DevWeave.Domain.Projects;

public sealed record ProjectReferenceId(Guid Value)
{
    public static ProjectReferenceId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}