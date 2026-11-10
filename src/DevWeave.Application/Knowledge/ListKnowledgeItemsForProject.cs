using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Knowledge;

public sealed class ListKnowledgeItemsForProject
{
    private readonly IKnowledgeItemRepository _items;

    public ListKnowledgeItemsForProject(IKnowledgeItemRepository items)
    {
        _items = items;
    }

    public Task<IReadOnlyList<KnowledgeItem>> ExecuteAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default) =>
        _items.ListByProjectAsync(tenantId, projectId, ct);
}