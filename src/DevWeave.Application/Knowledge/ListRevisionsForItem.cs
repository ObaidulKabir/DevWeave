using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;

namespace DevWeave.Application.Knowledge;

/// <summary>Lists every revision of an item in revision order so history can be read and compared (REV-001).</summary>
public sealed class ListRevisionsForItem
{
    private readonly IKnowledgeItemRepository _items;
    private readonly IKnowledgeRevisionRepository _revisions;

    public ListRevisionsForItem(IKnowledgeItemRepository items, IKnowledgeRevisionRepository revisions)
    {
        _items = items;
        _revisions = revisions;
    }

    public async Task<IReadOnlyList<KnowledgeRevision>> ExecuteAsync(
        TenantId tenantId, KnowledgeItemId itemId, CancellationToken ct = default)
    {
        _ = await _items.GetByIdAsync(tenantId, itemId, ct)
            ?? throw new NotFoundException($"Knowledge item '{itemId}' was not found within tenant '{tenantId}'.");

        var revisions = await _revisions.ListByItemAsync(tenantId, itemId, ct);
        return revisions.OrderBy(r => r.RevisionNumber).ToList();
    }
}
