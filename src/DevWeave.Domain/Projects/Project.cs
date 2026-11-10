using DevWeave.Domain.Identity;
using DevWeave.Domain.Lifecycle;

namespace DevWeave.Domain.Projects;

/// <summary>
/// An AI software delivery project owned by exactly one tenant (SRS PRJ-002). A project pins one
/// <see cref="LifecycleProfile"/> and moves through a governed lifecycle (PRJ-004) with recorded
/// archive/restore and a controlled, non-destructive delete (PRJ-009).
/// </summary>
public sealed class Project
{
    public ProjectId Id { get; }
    public TenantId TenantId { get; }
    public string Name { get; private set; }
    public string Key { get; }
    public string? Description { get; private set; }
    public ProjectStatus Status { get; private set; }
    public LifecycleProfileId LifecycleProfileId { get; }
    public PrincipalId CreatedBy { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }
    public DateTimeOffset? RestoredAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public bool IsDeleted { get; private set; }

    private Project(
        ProjectId id,
        TenantId tenantId,
        string name,
        string key,
        string? description,
        ProjectStatus status,
        LifecycleProfileId lifecycleProfileId,
        PrincipalId createdBy,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        Key = key;
        Description = description;
        Status = status;
        LifecycleProfileId = lifecycleProfileId;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public static Project Create(
        TenantId tenantId,
        string name,
        string key,
        string? description,
        LifecycleProfileId lifecycleProfileId,
        PrincipalId createdBy)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Project name is required.", nameof(name));

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Project key is required.", nameof(key));

        return new Project(
            ProjectId.New(),
            tenantId,
            name,
            key,
            description,
            ProjectStatus.Draft,
            lifecycleProfileId,
            createdBy,
            DateTimeOffset.UtcNow);
    }

    public void Activate() => TransitionTo(ProjectStatus.Active);

    public void OnHold() => TransitionTo(ProjectStatus.OnHold);

    public void Complete() => TransitionTo(ProjectStatus.Completed);

    public void Archive()
    {
        TransitionTo(ProjectStatus.Archived);
        ArchivedAt = DateTimeOffset.UtcNow;
    }

    public void Restore()
    {
        if (IsDeleted)
            throw new InvalidOperationException("A deleted project cannot be restored.");

        TransitionTo(ProjectStatus.Active);
        RestoredAt = DateTimeOffset.UtcNow;
    }

    public void Delete()
    {
        if (IsDeleted)
            throw new InvalidOperationException("Project is already deleted.");

        if (Status != ProjectStatus.Archived)
            throw new InvalidOperationException($"Only archived projects can be deleted (current status '{Status}').");

        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void TransitionTo(ProjectStatus target)
    {
        if (Status == target)
            throw new InvalidOperationException($"Project already has status '{target}'.");

        if (!IsAllowedTransition(Status, target))
            throw new InvalidOperationException($"Project cannot transition from '{Status}' to '{target}'.");

        Status = target;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static bool IsAllowedTransition(ProjectStatus from, ProjectStatus to) =>
        (from, to) switch
        {
            (ProjectStatus.Draft, ProjectStatus.Active) => true,
            (ProjectStatus.Draft, ProjectStatus.Archived) => true,
            (ProjectStatus.Active, ProjectStatus.OnHold) => true,
            (ProjectStatus.Active, ProjectStatus.Completed) => true,
            (ProjectStatus.Active, ProjectStatus.Archived) => true,
            (ProjectStatus.OnHold, ProjectStatus.Active) => true,
            (ProjectStatus.OnHold, ProjectStatus.Archived) => true,
            (ProjectStatus.Completed, ProjectStatus.Archived) => true,
            (ProjectStatus.Archived, ProjectStatus.Active) => true,
            _ => false,
        };
}