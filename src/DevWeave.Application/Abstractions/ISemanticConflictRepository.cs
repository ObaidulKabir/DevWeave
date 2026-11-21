using DevWeave.Domain.Identity;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Abstractions;

public interface ISemanticConflictRepository
{
    Task AddAsync(SemanticConflict conflict, CancellationToken ct = default);

    Task<SemanticConflict?> GetByIdAsync(
        TenantId tenantId,
        SemanticConflictId id,
        CancellationToken ct = default);

    Task<IReadOnlyList<SemanticConflict>> ListUnresolvedByProjectAsync(
        TenantId tenantId,
        ProjectId projectId,
        CancellationToken ct = default);
}
