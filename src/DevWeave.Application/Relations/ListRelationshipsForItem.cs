using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Relations;

namespace DevWeave.Application.Relations;

/// <summary>
/// Lists every relationship in which an item participates (as source or target), in traversal-stable
/// order. The item must be resolvable inside the tenant (deny-by-default); otherwise the read is
/// indistinguishable from an empty result.
/// </summary>
public sealed class ListRelationshipsForItem
{
    private readonly IRelationshipRepository _relationships;
    private readonly IKnowledgeItemRepository _items;

    public ListRelationshipsForItem(IRelationshipRepository relationships, IKnowledgeItemRepository items)
    {
        _relationships = relationships;
        _items = items;
    }

    public async Task<IReadOnlyList<Relationship>> ExecuteAsync(TenantId tenantId, KnowledgeItemId itemId, CancellationToken ct = default)
    {
        _ = await _items.GetByIdAsync(tenantId, itemId, ct)
            ?? throw new NotFoundException($"Knowledge item '{itemId}' was not found within tenant '{tenantId}'.");

        var outgoing = await _relationships.ListOutgoingAsync(tenantId, itemId, ct);
        var incoming = await _relationships.ListIncomingAsync(tenantId, itemId, ct);

        return outgoing
            .Concat(incoming)
            .GroupBy(r => r.Id)
            .Select(g => g.First())
            .OrderBy(r => r.CreatedAt)
            .ThenBy(r => r.Id.Value)
            .ToList();
    }
}