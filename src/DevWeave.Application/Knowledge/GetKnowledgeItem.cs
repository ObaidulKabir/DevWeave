using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;

namespace DevWeave.Application.Knowledge;

public sealed class GetKnowledgeItem
{
    private readonly IKnowledgeItemRepository _items;

    public GetKnowledgeItem(IKnowledgeItemRepository items)
    {
        _items = items;
    }

    public Task<KnowledgeItem?> ExecuteAsync(TenantId tenantId, KnowledgeItemId id, CancellationToken ct = default) =>
        _items.GetByIdAsync(tenantId, id, ct);
}