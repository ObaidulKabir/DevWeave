using DevWeave.Application.Abstractions;
using DevWeave.Application.Security;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Knowledge;

public sealed class ListKnowledgeItemsForProject
{
    private readonly IKnowledgeItemRepository _items;
    private readonly IProjectMembershipRepository _memberships;
    private readonly IMembershipRepository? _tenantMemberships;

    public ListKnowledgeItemsForProject(
        IKnowledgeItemRepository items,
        IProjectMembershipRepository memberships,
        IMembershipRepository? tenantMemberships = null)
    {
        _items = items;
        _memberships = memberships;
        _tenantMemberships = tenantMemberships;
    }

    public async Task<IReadOnlyList<KnowledgeItem>> ExecuteAsync(
        TenantId tenantId,
        ProjectId projectId,
        PrincipalId actorId,
        CancellationToken ct = default)
    {
        var role = await TenantProjectAccess.ResolveProjectRoleAsync(
            _memberships, tenantId, projectId, actorId, ct, _tenantMemberships);
        if (role is null)
            return Array.Empty<KnowledgeItem>();

        var ceiling = ClassificationAccess.CeilingFor(role.Value);
        var items = await _items.ListByProjectAsync(tenantId, projectId, ct);
        return items.Where(i => i.Classification <= ceiling).ToList();
    }
}
