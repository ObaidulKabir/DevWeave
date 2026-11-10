using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Lifecycle;

public sealed class CreateWorkItem
{
    private readonly IProjectRepository _projects;
    private readonly IPrincipalRepository _principals;
    private readonly IProjectMembershipRepository _memberships;
    private readonly IWorkPlanningRepository _work;
    private readonly IUnitOfWork _unitOfWork;

    public CreateWorkItem(IProjectRepository projects, IPrincipalRepository principals,
        IProjectMembershipRepository memberships, IWorkPlanningRepository work, IUnitOfWork unitOfWork)
    {
        _projects = projects;
        _principals = principals;
        _memberships = memberships;
        _work = work;
        _unitOfWork = unitOfWork;
    }

    public async Task<WorkItem> ExecuteAsync(TenantId tenantId, ProjectId projectId,
        string key, string title, string description, MilestoneId? milestoneId, PrincipalId? ownerId,
        DateTimeOffset? dueAt, WorkPriority priority, PrincipalId actorId,
        IReadOnlyList<WorkItemId> dependsOn, CancellationToken ct = default)
    {
        _ = await _projects.GetByIdAsync(tenantId, projectId, ct)
            ?? throw new NotFoundException($"Project '{projectId}' was not found within tenant '{tenantId}'.");
        var actor = await _principals.GetByIdAsync(tenantId, actorId, ct);
        if (actor is null || !actor.IsActive)
            throw new NotFoundException($"Principal '{actorId}' was not found within tenant '{tenantId}'.");
        await ProjectWorkAuthorization.EnsureMemberAsync(_memberships, tenantId, projectId, actorId,
            reviewerOnly: false, ct);
        if (ownerId is not null)
        {
            var owner = await _principals.GetByIdAsync(tenantId, ownerId, ct);
            if (owner is null || !owner.IsActive)
                throw new NotFoundException($"Owner '{ownerId}' was not found within tenant '{tenantId}'.");
            await ProjectWorkAuthorization.EnsureMemberAsync(_memberships, tenantId, projectId, ownerId,
                reviewerOnly: false, ct);
        }

        if (await _work.WorkItemKeyExistsAsync(tenantId, projectId, key, ct))
            throw new ConflictException($"Work item key '{key}' already exists in this project.");
        if (milestoneId is not null &&
            await _work.GetMilestoneAsync(tenantId, projectId, milestoneId, ct) is null)
            throw new NotFoundException($"Milestone '{milestoneId}' was not found within this project.");

        var dependencies = dependsOn.Distinct().ToArray();
        if (dependencies.Length != dependsOn.Count)
            throw new ConflictException("A dependency may only be listed once.");
        foreach (var dependencyId in dependencies)
        {
            if (await _work.GetWorkItemAsync(tenantId, projectId, dependencyId, ct) is null)
                throw new NotFoundException($"Dependency '{dependencyId}' was not found within this project.");
        }

        var item = WorkItem.Create(tenantId, projectId, key, title, description, milestoneId,
            ownerId, dueAt, priority, actorId);
        await _work.AddWorkItemAsync(item, ct);
        foreach (var dependencyId in dependencies)
        {
            await _work.AddDependencyAsync(
                WorkItemDependency.Create(tenantId, projectId, item.Id, dependencyId, actorId), ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return item;
    }
}
