using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Abstractions;

public interface IProjectMembershipRepository
{
    Task AddAsync(ProjectMembership membership, CancellationToken ct = default);

    Task<ProjectMembership?> GetByIdAsync(TenantId tenantId, ProjectMembershipId id, CancellationToken ct = default);

    Task<IReadOnlyList<ProjectMembership>> ListByProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default);

    Task<IReadOnlyList<ProjectMembership>> ListByPrincipalAsync(TenantId tenantId, PrincipalId principalId, CancellationToken ct = default);
}