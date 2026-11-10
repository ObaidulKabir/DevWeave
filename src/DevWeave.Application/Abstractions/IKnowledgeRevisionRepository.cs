using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;

namespace DevWeave.Application.Abstractions;

public interface IKnowledgeRevisionRepository
{
    Task AddAsync(KnowledgeRevision revision, CancellationToken ct = default);

    Task<KnowledgeRevision?> GetByIdAsync(TenantId tenantId, KnowledgeRevisionId id, CancellationToken ct = default);

    Task<IReadOnlyList<KnowledgeRevision>> ListByItemAsync(TenantId tenantId, KnowledgeItemId itemId, CancellationToken ct = default);
}