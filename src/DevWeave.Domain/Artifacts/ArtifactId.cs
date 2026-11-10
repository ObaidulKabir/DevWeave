namespace DevWeave.Domain.Artifacts;

public sealed record ArtifactId(Guid Value)
{
    public static ArtifactId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}