using DevWeave.Application.Abstractions;
using DevWeave.Application.Security;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Knowledge;

public sealed class GetKnowledgeItem
{
    private readonly IKnowledgeItemRepository _items;
    private readonly IProjectMembershipRepository _memberships;

    public GetKnowledgeItem(IKnowledgeItemRepository items, IProjectMembershipRepository memberships)
    {
        _items = items;
        _memberships = memberships;
    }

    public async Task<KnowledgeItem?> ExecuteAsync(
        TenantId tenantId,
        KnowledgeItemId id,
        PrincipalId actorId,
        CancellationToken ct = default)
    {
        var item = await _items.GetByIdAsync(tenantId, id, ct);
        if (item is null)
            return null;

        var memberships = await _memberships.ListByPrincipalAsync(tenantId, actorId, ct);
        var membership = memberships.FirstOrDefault(m =>
            m.ProjectId == item.ProjectId && m.Status == ProjectMembershipStatus.Active);
        if (membership is null || !ClassificationAccess.Allows(membership.Role, item.Classification))
            return null;

        return item;
    }
}
