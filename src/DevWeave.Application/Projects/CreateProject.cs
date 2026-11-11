using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Projects;

public sealed class CreateProject
{
    private readonly IProjectRepository _projects;
    private readonly ILifecycleProfileRepository _profiles;
    private readonly IPrincipalRepository _principals;
    private readonly IProjectMembershipRepository _memberships;
    private readonly IUnitOfWork _unitOfWork;

    public CreateProject(
        IProjectRepository projects,
        ILifecycleProfileRepository profiles,
        IPrincipalRepository principals,
        IProjectMembershipRepository memberships,
        IUnitOfWork unitOfWork)
    {
        _projects = projects;
        _profiles = profiles;
        _principals = principals;
        _memberships = memberships;
        _unitOfWork = unitOfWork;
    }

    public async Task<Project> ExecuteAsync(
        TenantId tenantId,
        string name,
        string key,
        string? description,
        LifecycleProfileId lifecycleProfileId,
        PrincipalId createdBy,
        CancellationToken ct = default)
    {
        // Deny-by-default: a profile from another tenant is indistinguishable from a missing one.
        var profile = await _profiles.GetByIdAsync(tenantId, lifecycleProfileId, ct)
            ?? throw new NotFoundException($"Lifecycle profile '{lifecycleProfileId}' was not found within tenant '{tenantId}'.");

        // Deny-by-default: only an actor resolvable inside the tenant may create a project (PRJ-001).
        var principal = await _principals.GetByIdAsync(tenantId, createdBy, ct)
            ?? throw new NotFoundException($"Principal '{createdBy}' was not found within tenant '{tenantId}'.");

        if (await _projects.KeyExistsAsync(tenantId, key, ct))
            throw new ConflictException($"A project with key '{key}' already exists within tenant '{tenantId}'.");

        var project = Project.Create(tenantId, name, key, description, profile.Id, principal.Id);

        // Creator becomes the first project owner so subsequent membership grants are authorized
        // under RequireTenantMembership without a chicken-and-egg gap.
        var ownerMembership = ProjectMembership.Grant(
            tenantId, project.Id, principal.Id, ProjectRole.Owner);

        await _projects.AddAsync(project, ct);
        await _memberships.AddAsync(ownerMembership, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return project;
    }
}