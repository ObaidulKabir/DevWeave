using DevWeave.Application.Abstractions;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Domain.Identity;

namespace DevWeave.Application.Identity;

/// <summary>Issues and revokes time-bounded platform support access (TEN-009).</summary>
public sealed class ManageSupportAccess
{
    private readonly ISupportAccessGrantRepository _grants;
    private readonly IPrincipalRepository _principals;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditJournal _audit;

    public ManageSupportAccess(
        ISupportAccessGrantRepository grants,
        IPrincipalRepository principals,
        IUnitOfWork unitOfWork,
        IAuditJournal audit)
    {
        _grants = grants;
        _principals = principals;
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    public async Task<SupportAccessGrant> IssueAsync(
        TenantId tenantId,
        PrincipalId operatorId,
        PrincipalId approverId,
        string purpose,
        TimeSpan lifetime,
        CancellationToken ct = default)
    {
        _ = await _principals.GetByIdAsync(tenantId, operatorId, ct)
            ?? throw new NotFoundException($"Operator principal '{operatorId}' was not found within tenant '{tenantId}'.");
        _ = await _principals.GetByIdAsync(tenantId, approverId, ct)
            ?? throw new NotFoundException($"Approver principal '{approverId}' was not found within tenant '{tenantId}'.");

        var existing = await _grants.GetActiveAsync(tenantId, operatorId, DateTimeOffset.UtcNow, ct);
        if (existing is not null)
            throw new ConflictException("An active support access grant already exists for this operator.");

        var grant = SupportAccessGrant.Issue(tenantId, operatorId, approverId, purpose, lifetime);
        await _grants.AddAsync(grant, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        await _audit.AppendAsync(
            new AuthorizedScope(tenantId, approverId, null, "IssueSupportAccess", grant.Id.Value.ToString("D")),
            "SupportAccessIssued",
            $"Grant:{grant.Id.Value}",
            $"Operator:{operatorId.Value};Expires:{grant.ExpiresAt:O}",
            ct);

        return grant;
    }

    public async Task RevokeAsync(
        TenantId tenantId,
        SupportAccessGrantId grantId,
        PrincipalId actorId,
        CancellationToken ct = default)
    {
        var grant = await _grants.GetByIdAsync(tenantId, grantId, ct)
            ?? throw new NotFoundException($"Support access grant '{grantId}' was not found.");

        grant.Revoke();
        await _unitOfWork.SaveChangesAsync(ct);

        await _audit.AppendAsync(
            new AuthorizedScope(tenantId, actorId, null, "RevokeSupportAccess", grant.Id.Value.ToString("D")),
            "SupportAccessRevoked",
            $"Grant:{grant.Id.Value}",
            "Revoked",
            ct);
    }

    public Task<IReadOnlyList<SupportAccessGrant>> ListAsync(TenantId tenantId, CancellationToken ct = default) =>
        _grants.ListByTenantAsync(tenantId, ct);
}
