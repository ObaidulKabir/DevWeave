namespace DevWeave.Domain.Identity;

public sealed class Tenant
{
    public TenantId Id { get; }
    public string Name { get; private set; }
    public DateTimeOffset CreatedAt { get; }

    private Tenant(TenantId id, string name, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        CreatedAt = createdAt;
    }

    public static Tenant Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tenant name is required.", nameof(name));

        return new Tenant(TenantId.New(), name, DateTimeOffset.UtcNow);
    }
}
