using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;

namespace DevWeave.Application.Knowledge;

public sealed class ListKnowledgeItemsForTenant
{
    private readonly IKnowledgeItemRepository _items;

    public ListKnowledgeItemsForTenant(IKnowledgeItemRepository items)
    {
        _items = items;
    }

    public Task<IReadOnlyList<KnowledgeItem>> ExecuteAsync(TenantId tenantId, CancellationToken ct = default) =>
        _items.ListByTenantAsync(tenantId, ct);
}