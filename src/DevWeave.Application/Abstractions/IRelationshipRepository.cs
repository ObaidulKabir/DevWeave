using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Relations;

namespace DevWeave.Application.Abstractions;

/// <summary>
/// Persistence port for tenant-owned relationships (SRS REL-001). Every read is tenant-scoped and
/// never discloses another tenant's edges. Revocation is performed on the loaded aggregate via its
/// <see cref="Relationship.Revoke"/> method, followed by a unit-of-work save.
/// </summary>
public interface IRelationshipRepository
{
    Task AddAsync(Relationship relationship, CancellationToken ct = default);

    Task<Relationship?> GetByIdAsync(TenantId tenantId, RelationshipId id, CancellationToken ct = default);

    Task<IReadOnlyList<Relationship>> ListByProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default);

    Task<IReadOnlyList<Relationship>> ListOutgoingAsync(TenantId tenantId, KnowledgeItemId itemId, CancellationToken ct = default);

    Task<IReadOnlyList<Relationship>> ListIncomingAsync(TenantId tenantId, KnowledgeItemId itemId, CancellationToken ct = default);
}