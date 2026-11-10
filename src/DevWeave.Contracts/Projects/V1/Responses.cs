using DevWeave.Domain.Projects;

namespace DevWeave.Contracts.Projects.V1;

public sealed record ProjectResponse(
    Guid Id,
    Guid TenantId,
    string Name,
    string Key,
    string? Description,
    ProjectStatus Status,
    Guid LifecycleProfileId,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? ArchivedAt,
    DateTimeOffset? RestoredAt,
    DateTimeOffset? DeletedAt,
    bool IsDeleted);

public sealed record ProjectReferenceResponse(
    Guid Id,
    Guid TenantId,
    Guid ProjectId,
    ProjectReferenceType Type,
    string ExternalId,
    string? Url,
    DateTimeOffset CreatedAt);

public sealed record ProjectMembershipResponse(
    Guid Id,
    Guid TenantId,
    Guid ProjectId,
    Guid PrincipalId,
    ProjectRole Role,
    ProjectMembershipStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RevokedAt);