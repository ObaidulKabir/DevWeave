using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;

namespace DevWeave.Application.Identity;

/// <summary>
/// Provisions a tenant and its first human Owner in one save. Email delivery is intentionally not
/// part of this step; the owner is active as soon as registration succeeds.
/// </summary>
public sealed class RegisterTenant
{
    private readonly ITenantRepository _tenants;
    private readonly IPrincipalRepository _principals;
    private readonly IMembershipRepository _memberships;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantContext _tenantContext;

    public RegisterTenant(
        ITenantRepository tenants,
        IPrincipalRepository principals,
        IMembershipRepository memberships,
        IUnitOfWork unitOfWork,
        ITenantContext tenantContext)
    {
        _tenants = tenants;
        _principals = principals;
        _memberships = memberships;
        _unitOfWork = unitOfWork;
        _tenantContext = tenantContext;
    }

    public async Task<RegisteredTenant> ExecuteAsync(string tenantName, string ownerEmail, CancellationToken ct = default)
    {
        var name = tenantName?.Trim() ?? string.Empty;
        var email = ownerEmail?.Trim() ?? string.Empty;

        if (name.Length is < 2 or > 200)
            throw new ArgumentException("Tenant name must be between 2 and 200 characters.", nameof(tenantName));

        if (email.Length > 256 || !IsSingleEmailAddress(email))
            throw new ArgumentException("Owner email must be a single address of at most 256 characters.", nameof(ownerEmail));

        var tenant = Tenant.Create(name);
        // Principals and memberships are behind row-level security. The session tenant must be
        // this new tenant before the connection opens, or the application role's insert is rejected.
        _tenantContext.TenantId = tenant.Id.Value;
        var owner = Principal.Create(tenant.Id, email, PrincipalType.Human);
        var membership = Membership.Invite(tenant.Id, owner.Id, MembershipRole.Owner);
        membership.Activate();

        await _tenants.AddAsync(tenant, ct);
        await _principals.AddAsync(owner, ct);
        await _memberships.AddAsync(membership, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new RegisteredTenant(tenant, owner, membership);
    }

    private static bool IsSingleEmailAddress(string email)
    {
        var at = email.IndexOf('@');
        return at > 0
            && at == email.LastIndexOf('@')
            && at < email.Length - 1
            && !email.Any(char.IsWhiteSpace);
    }
}

public sealed record RegisteredTenant(Tenant Tenant, Principal Owner, Membership Membership);
