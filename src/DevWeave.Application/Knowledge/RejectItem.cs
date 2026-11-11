using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;

namespace DevWeave.Application.Knowledge;

/// <summary>
/// Revision-agnostic lifecycle transition that records no reviewer. The HTTP surface does not use it:
/// human decisions go through <see cref="ReviewKnowledge"/>, which binds the decision to one revision
/// and writes an audit record. Kept for internal callers and existing tests.
/// </summary>
public sealed class RejectItem
{
    private readonly IKnowledgeItemRepository _items;
    private readonly IUnitOfWork _unitOfWork;

    public RejectItem(IKnowledgeItemRepository items, IUnitOfWork unitOfWork)
    {
        _items = items;
        _unitOfWork = unitOfWork;
    }

    public async Task<KnowledgeItem> ExecuteAsync(TenantId tenantId, KnowledgeItemId id, CancellationToken ct = default)
    {
        var item = await _items.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Knowledge item '{id}' was not found within tenant '{tenantId}'.");

        item.Reject();

        await _unitOfWork.SaveChangesAsync(ct);

        return item;
    }
}