using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Lifecycle;

public sealed record WorkItemChange(WorkItem Item, IReadOnlyList<WorkItemId> DependsOn,
    IReadOnlyList<WorkItemTransition> Transitions);

public sealed class TransitionWorkItem
{
    private readonly IWorkPlanningRepository _work;
    private readonly IPrincipalRepository _principals;
    private readonly IProjectMembershipRepository _memberships;
    private readonly IUnitOfWork _unitOfWork;

    public TransitionWorkItem(IWorkPlanningRepository work, IPrincipalRepository principals,
        IProjectMembershipRepository memberships, IUnitOfWork unitOfWork)
    {
        _work = work;
        _principals = principals;
        _memberships = memberships;
        _unitOfWork = unitOfWork;
    }

    public Task<WorkItemChange> ChangePlannedAsync(TenantId tenantId, ProjectId projectId,
        WorkItemId itemId, PlannedWorkState state, PrincipalId actorId, string reason,
        int expectedVersion, CancellationToken ct = default) =>
        ExecuteAsync(tenantId, projectId, itemId, actorId, reviewerOnly: false, ct, item =>
            [item.ChangePlannedState(state, actorId, reason, expectedVersion)]);

    public Task<WorkItemChange> ReportAsync(TenantId tenantId, ProjectId projectId,
        WorkItemId itemId, ReportedExecutionState state, PrincipalId actorId, string reason,
        int expectedVersion, CancellationToken ct = default) =>
        ExecuteAsync(tenantId, projectId, itemId, actorId, reviewerOnly: false, ct,
            item => item.Report(state, actorId, reason, expectedVersion));

    public Task<WorkItemChange> VerifyAsync(TenantId tenantId, ProjectId projectId,
        WorkItemId itemId, PrincipalId actorId, string reason, int expectedVersion,
        CancellationToken ct = default) =>
        ExecuteAsync(tenantId, projectId, itemId, actorId, reviewerOnly: true, ct,
            item => [item.Verify(actorId, reason, expectedVersion)]);

    public Task<WorkItemChange> ApproveAsync(TenantId tenantId, ProjectId projectId,
        WorkItemId itemId, PrincipalId actorId, string reason, int expectedVersion,
        CancellationToken ct = default) =>
        ExecuteAsync(tenantId, projectId, itemId, actorId, reviewerOnly: true, ct,
            item => [item.Approve(actorId, reason, expectedVersion)]);

    public Task<WorkItemChange> RejectApprovalAsync(TenantId tenantId, ProjectId projectId,
        WorkItemId itemId, PrincipalId actorId, string reason, int expectedVersion,
        CancellationToken ct = default) =>
        ExecuteAsync(tenantId, projectId, itemId, actorId, reviewerOnly: true, ct,
            item => [item.RejectApproval(actorId, reason, expectedVersion)]);

    private async Task<WorkItemChange> ExecuteAsync(TenantId tenantId, ProjectId projectId,
        WorkItemId itemId, PrincipalId actorId, bool reviewerOnly, CancellationToken ct,
        Func<WorkItem, IReadOnlyList<WorkItemTransition>> transition)
    {
        var principal = await _principals.GetByIdAsync(tenantId, actorId, ct);
        if (principal is null || !principal.IsActive ||
            (reviewerOnly && principal.Type != PrincipalType.Human))
            throw new NotFoundException($"Principal '{actorId}' was not found within tenant '{tenantId}'.");
        await ProjectWorkAuthorization.EnsureMemberAsync(_memberships, tenantId, projectId, actorId,
            reviewerOnly, ct);
        var item = await _work.GetWorkItemAsync(tenantId, projectId, itemId, ct)
            ?? throw new NotFoundException($"Work item '{itemId}' was not found within project '{projectId}'.");
        var events = transition(item);
        await _work.AddTransitionsAsync(events, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        var dependencies = await _work.ListDependenciesAsync(tenantId, projectId, ct);
        return new WorkItemChange(item,
            dependencies.Where(dependency => dependency.WorkItemId == itemId)
                .Select(dependency => dependency.DependsOnId).ToArray(),
            events);
    }
}
