using DevWeave.Domain.Identity;

namespace DevWeave.Contracts.Identity.V1;

public sealed record CreateTenantRequest(string Name);

public sealed record CreatePrincipalRequest(string Email, PrincipalType Type);

public sealed record InviteMemberRequest(Guid PrincipalId, MembershipRole Role);