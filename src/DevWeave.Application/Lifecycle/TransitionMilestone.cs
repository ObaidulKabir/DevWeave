using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Lifecycle;

public sealed class TransitionMilestone(IProjectMembershipRepository memberships,
    IPrincipalRepository principals, IWorkPlanningRepository work, IUnitOfWork unitOfWork)
{
    public async Task<MilestoneTransition> ExecuteAsync(TenantId tenantId, ProjectId projectId,
        MilestoneId milestoneId, MilestoneState target, PrincipalId actorId, string reason,
        int expectedVersion, CancellationToken ct = default)
    {
        var principal = await principals.GetByIdAsync(tenantId, actorId, ct);
        if (principal is null || !principal.IsActive || principal.Type != PrincipalType.Human)
            throw new NotFoundException($"Principal '{actorId}' was not found within tenant '{tenantId}'.");
        await ProjectWorkAuthorization.EnsureMemberAsync(memberships, tenantId, projectId, actorId,
            reviewerOnly: true, ct);

        var milestone = await work.GetMilestoneAsync(tenantId, projectId, milestoneId, ct)
            ?? throw new NotFoundException($"Milestone '{milestoneId}' was not found within project '{projectId}'.");
        var transition = milestone.ChangeState(target, actorId, reason, expectedVersion);
        await work.AddMilestoneTransitionAsync(transition, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return transition;
    }
}
