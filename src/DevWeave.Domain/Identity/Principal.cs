namespace DevWeave.Domain.Identity;

public sealed class Principal
{
    public PrincipalId Id { get; }
    public TenantId TenantId { get; }
    public string Email { get; private set; }
    public PrincipalType Type { get; }
    public bool IsActive { get; private set; }

    private Principal(PrincipalId id, TenantId tenantId, string email, PrincipalType type, bool isActive)
    {
        Id = id;
        TenantId = tenantId;
        Email = email;
        Type = type;
        IsActive = isActive;
    }

    public static Principal Create(TenantId tenantId, string email, PrincipalType type)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));

        return new Principal(PrincipalId.New(), tenantId, email, type, isActive: true);
    }

    public void Revoke()
    {
        IsActive = false;
    }
}
