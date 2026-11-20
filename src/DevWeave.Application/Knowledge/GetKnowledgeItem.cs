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
    private readonly IMembershipRepository? _tenantMemberships;

    public GetKnowledgeItem(
        IKnowledgeItemRepository items,
        IProjectMembershipRepository memberships,
        IMembershipRepository? tenantMemberships = null)
    {
        _items = items;
        _memberships = memberships;
        _tenantMemberships = tenantMemberships;
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

        var role = await TenantProjectAccess.ResolveProjectRoleAsync(
            _memberships, tenantId, item.ProjectId, actorId, ct, _tenantMemberships);
        if (role is null || !ClassificationAccess.Allows(role.Value, item.Classification))
            return null;

        return item;
    }
}
