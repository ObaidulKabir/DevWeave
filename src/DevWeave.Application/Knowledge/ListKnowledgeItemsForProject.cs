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

    public ListKnowledgeItemsForProject(
        IKnowledgeItemRepository items,
        IProjectMembershipRepository memberships)
    {
        _items = items;
        _memberships = memberships;
    }

    public async Task<IReadOnlyList<KnowledgeItem>> ExecuteAsync(
        TenantId tenantId,
        ProjectId projectId,
        PrincipalId actorId,
        CancellationToken ct = default)
    {
        var memberships = await _memberships.ListByPrincipalAsync(tenantId, actorId, ct);
        var membership = memberships.FirstOrDefault(m =>
            m.ProjectId == projectId && m.Status == ProjectMembershipStatus.Active);
        if (membership is null)
            return Array.Empty<KnowledgeItem>();

        var ceiling = ClassificationAccess.CeilingFor(membership.Role);
        var items = await _items.ListByProjectAsync(tenantId, projectId, ct);
        return items.Where(i => i.Classification <= ceiling).ToList();
    }
}
