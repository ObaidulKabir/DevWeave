using DevWeave.Domain.Identity;

namespace DevWeave.Contracts.Identity.V1;

public sealed record TenantResponse(Guid Id, string Name, DateTimeOffset CreatedAt);

public sealed record PrincipalResponse(Guid Id, Guid TenantId, string Email, PrincipalType Type, bool IsActive);

public sealed record MembershipResponse(
    Guid Id,
    Guid TenantId,
    Guid PrincipalId,
    MembershipRole Role,
    MembershipStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset? RevokedAt);