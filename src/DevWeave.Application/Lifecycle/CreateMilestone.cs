using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Lifecycle;

public sealed class CreateMilestone
{
    private readonly IProjectRepository _projects;
    private readonly IPrincipalRepository _principals;
    private readonly IProjectMembershipRepository _memberships;
    private readonly IWorkPlanningRepository _work;
    private readonly IUnitOfWork _unitOfWork;

    public CreateMilestone(IProjectRepository projects, IPrincipalRepository principals,
        IProjectMembershipRepository memberships, IWorkPlanningRepository work, IUnitOfWork unitOfWork)
    {
        _projects = projects;
        _principals = principals;
        _memberships = memberships;
        _work = work;
        _unitOfWork = unitOfWork;
    }

    public async Task<Milestone> ExecuteAsync(TenantId tenantId, ProjectId projectId, string key,
        string title, DateTimeOffset? dueAt, PrincipalId actorId, CancellationToken ct = default)
    {
        _ = await _projects.GetByIdAsync(tenantId, projectId, ct)
            ?? throw new NotFoundException($"Project '{projectId}' was not found within tenant '{tenantId}'.");
        var principal = await _principals.GetByIdAsync(tenantId, actorId, ct);
        if (principal is null || !principal.IsActive)
            throw new NotFoundException($"Principal '{actorId}' was not found within tenant '{tenantId}'.");
        await ProjectWorkAuthorization.EnsureMemberAsync(_memberships, tenantId, projectId, actorId,
            reviewerOnly: false, ct);

        if (await _work.MilestoneKeyExistsAsync(tenantId, projectId, key, ct))
            throw new ConflictException($"Milestone key '{key}' already exists in this project.");

        var milestone = Milestone.Create(tenantId, projectId, key, title, dueAt, actorId);
        await _work.AddMilestoneAsync(milestone, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return milestone;
    }
}
