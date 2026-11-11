using DevWeave.Domain.Identity;

namespace DevWeave.Domain.Integrations;

public readonly record struct IntegrationId(Guid Value)
{
    public static IntegrationId New() => new(Guid.NewGuid());
    public static implicit operator Guid(IntegrationId id) => id.Value;
}
