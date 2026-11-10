using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Lifecycle;

public sealed class ListMilestones(IWorkPlanningRepository work, IPrincipalRepository principals,
    IProjectMembershipRepository memberships)
{
    public Task<IReadOnlyList<Milestone>> ExecuteAsync(TenantId tenantId, ProjectId projectId,
        PrincipalId actorId, CancellationToken ct = default) =>
        ExecuteScopedAsync(tenantId, projectId, actorId, ct);

    private async Task<IReadOnlyList<Milestone>> ExecuteScopedAsync(TenantId tenantId,
        ProjectId projectId, PrincipalId actorId, CancellationToken ct)
    {
        await ProjectWorkAuthorization.EnsureCanReadAsync(principals, memberships,
            tenantId, projectId, actorId, ct);
        return await work.ListMilestonesAsync(tenantId, projectId, ct);
    }
}
