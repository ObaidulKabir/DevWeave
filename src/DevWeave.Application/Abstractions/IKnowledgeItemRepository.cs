using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Abstractions;

public interface IKnowledgeItemRepository
{
    Task AddAsync(KnowledgeItem item, CancellationToken ct = default);

    Task<KnowledgeItem?> GetByIdAsync(TenantId tenantId, KnowledgeItemId id, CancellationToken ct = default);

    Task<IReadOnlyList<KnowledgeItem>> ListByProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default);

    Task<IReadOnlyList<KnowledgeItem>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default);

    Task<bool> KeyExistsAsync(TenantId tenantId, ProjectId projectId, string key, CancellationToken ct = default);
}