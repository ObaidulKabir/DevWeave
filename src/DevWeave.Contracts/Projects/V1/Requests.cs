using DevWeave.Domain.Projects;

namespace DevWeave.Contracts.Projects.V1;

public sealed record CreateProjectRequest(
    string Name,
    string Key,
    string? Description,
    Guid LifecycleProfileId,
    Guid CreatedBy);

public sealed record AddProjectReferenceRequest(
    ProjectReferenceType Type,
    string ExternalId,
    string? Url);

public sealed record GrantProjectMembershipRequest(
    Guid PrincipalId,
    ProjectRole Role);