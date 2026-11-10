using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Lifecycle;

public sealed class ListMilestoneTransitions(IPrincipalRepository principals,
    IProjectMembershipRepository memberships, IWorkPlanningRepository work)
{
    public async Task<IReadOnlyList<MilestoneTransition>> ExecuteAsync(TenantId tenantId,
        ProjectId projectId, MilestoneId milestoneId, PrincipalId actorId, CancellationToken ct = default)
    {
        await ProjectWorkAuthorization.EnsureCanReadAsync(principals, memberships,
            tenantId, projectId, actorId, ct);
        if (await work.GetMilestoneAsync(tenantId, projectId, milestoneId, ct) is null)
            throw new NotFoundException($"Milestone '{milestoneId}' was not found within project '{projectId}'.");
        return await work.ListMilestoneTransitionsAsync(tenantId, projectId, milestoneId, ct);
    }
}
