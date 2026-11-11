namespace DevWeave.Domain.Integrations;

public readonly record struct InboundEventId(Guid Value)
{
    public static InboundEventId New() => new(Guid.NewGuid());
    public static implicit operator Guid(InboundEventId id) => id.Value;
}
