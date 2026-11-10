namespace DevWeave.Domain.Evidence;

public sealed record EvidenceId(Guid Value)
{
    public static EvidenceId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}