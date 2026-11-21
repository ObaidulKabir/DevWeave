using DevWeave.Application.Abstractions;
using DevWeave.Application.Security;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Relations;

namespace DevWeave.Application.Relations;

/// <summary>
/// Applies project membership and classification policy before a relationship can be returned or
/// traversed. An edge is visible only when both endpoint items are visible to the caller.
/// </summary>
public sealed class RelationshipReadAuthorization
{
    private readonly IProjectMembershipRepository _projectMemberships;
    private readonly IMembershipRepository _tenantMemberships;
    private readonly IKnowledgeItemRepository _items;

    public RelationshipReadAuthorization(
        IProjectMembershipRepository projectMemberships,
        IMembershipRepository tenantMemberships,
        IKnowledgeItemRepository items)
    {
        _projectMemberships = projectMemberships;
        _tenantMemberships = tenantMemberships;
        _items = items;
    }

    public Task<ProjectRole?> ResolveRoleAsync(
        TenantId tenantId,
        ProjectId projectId,
        PrincipalId actorId,
        CancellationToken ct = default) =>
        TenantProjectAccess.ResolveProjectRoleAsync(
            _projectMemberships,
            tenantId,
            projectId,
            actorId,
            ct,
            _tenantMemberships);

    public async Task<(KnowledgeItem Item, ProjectRole Role)> RequireVisibleItemAsync(
        TenantId tenantId,
        KnowledgeItemId itemId,
        PrincipalId actorId,
        CancellationToken ct = default)
    {
        var item = await _items.GetByIdAsync(tenantId, itemId, ct)
            ?? throw HiddenItem(itemId);

        var role = await ResolveRoleAsync(tenantId, item.ProjectId, actorId, ct);
        if (role is null || !ClassificationAccess.Allows(role.Value, item.Classification))
            throw HiddenItem(itemId);

        return (item, role.Value);
    }

    public async Task<IReadOnlyList<Relationship>> FilterProjectRelationshipsAsync(
        TenantId tenantId,
        ProjectId projectId,
        ProjectRole role,
        IReadOnlyList<Relationship> relationships,
        CancellationToken ct = default)
    {
        var visibleIds = (await _items.ListByProjectAsync(tenantId, projectId, ct))
            .Where(item => ClassificationAccess.Allows(role, item.Classification))
            .Select(item => item.Id)
            .ToHashSet();

        return relationships
            .Where(relationship =>
                visibleIds.Contains(relationship.SourceItemId) &&
                visibleIds.Contains(relationship.TargetItemId))
            .ToList();
    }

    public async Task<bool> IsRelationshipVisibleAsync(
        TenantId tenantId,
        Relationship relationship,
        ProjectRole role,
        CancellationToken ct = default)
    {
        var source = await _items.GetByIdAsync(tenantId, relationship.SourceItemId, ct);
        var target = await _items.GetByIdAsync(tenantId, relationship.TargetItemId, ct);

        return source is not null &&
               target is not null &&
               source.ProjectId == relationship.ProjectId &&
               target.ProjectId == relationship.ProjectId &&
               ClassificationAccess.Allows(role, source.Classification) &&
               ClassificationAccess.Allows(role, target.Classification);
    }

    private static NotFoundException HiddenItem(KnowledgeItemId itemId) =>
        new($"Knowledge item '{itemId}' was not found.");
}
