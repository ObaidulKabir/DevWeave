using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;

namespace DevWeave.Application.Knowledge;

public sealed class ApproveItem
{
    private readonly IKnowledgeItemRepository _items;
    private readonly IUnitOfWork _unitOfWork;

    public ApproveItem(IKnowledgeItemRepository items, IUnitOfWork unitOfWork)
    {
        _items = items;
        _unitOfWork = unitOfWork;
    }

    public async Task<KnowledgeItem> ExecuteAsync(TenantId tenantId, KnowledgeItemId id, CancellationToken ct = default)
    {
        var item = await _items.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Knowledge item '{id}' was not found within tenant '{tenantId}'.");

        item.Approve();

        await _unitOfWork.SaveChangesAsync(ct);

        return item;
    }
}