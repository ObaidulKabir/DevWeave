using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Lifecycle;

public sealed class ListWorkItemTransitions(IWorkPlanningRepository work, IPrincipalRepository principals,
    IProjectMembershipRepository memberships)
{
    public async Task<IReadOnlyList<WorkItemTransition>> ExecuteAsync(TenantId tenantId,
        ProjectId projectId, WorkItemId itemId, PrincipalId actorId, CancellationToken ct = default)
    {
        await ProjectWorkAuthorization.EnsureCanReadAsync(principals, memberships,
            tenantId, projectId, actorId, ct);
        return await work.ListTransitionsAsync(tenantId, projectId, itemId, ct);
    }
}
