using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Lifecycle;

public sealed class ListWorkItems(IWorkPlanningRepository work, IPrincipalRepository principals,
    IProjectMembershipRepository memberships)
{
    public async Task<IReadOnlyList<WorkItemDetails>> ExecuteAsync(TenantId tenantId,
        ProjectId projectId, PrincipalId actorId, CancellationToken ct = default)
    {
        await ProjectWorkAuthorization.EnsureCanReadAsync(principals, memberships,
            tenantId, projectId, actorId, ct);
        var items = await work.ListWorkItemsAsync(tenantId, projectId, ct);
        var dependencies = await work.ListDependenciesAsync(tenantId, projectId, ct);
        var byItem = dependencies.GroupBy(d => d.WorkItemId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<WorkItemId>)
                group.Select(dependency => dependency.DependsOnId).ToArray());
        return items.Select(item => new WorkItemDetails(item,
                byItem.GetValueOrDefault(item.Id, Array.Empty<WorkItemId>())))
            .ToArray();
    }
}
