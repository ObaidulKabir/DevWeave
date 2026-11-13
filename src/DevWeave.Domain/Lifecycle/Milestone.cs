using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Lifecycle;

public sealed class Milestone
{
    public MilestoneId Id { get; }
    public TenantId TenantId { get; }
    public ProjectId ProjectId { get; }
    public string Key { get; }
    public string Title { get; private set; }
    public DateTimeOffset? DueAt { get; private set; }
    public MilestoneState State { get; private set; }
    public PrincipalId CreatedBy { get; }
    public DateTimeOffset CreatedAt { get; }
    public int Version { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Milestone()
    {
        Id = null!;
        TenantId = null!;
        ProjectId = null!;
        Key = null!;
        Title = null!;
        CreatedBy = null!;
    }

    private Milestone(MilestoneId id, TenantId tenantId, ProjectId projectId, string key,
        string title, DateTimeOffset? dueAt, PrincipalId createdBy, DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        Key = key;
        Title = title;
        DueAt = dueAt;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        State = MilestoneState.Planned;
        Version = 1;
        UpdatedAt = createdAt;
    }

    public static Milestone Create(TenantId tenantId, ProjectId projectId, string key,
        string title, DateTimeOffset? dueAt, PrincipalId createdBy)
    {
        Validate(key, nameof(key), 100);
        Validate(title, nameof(title), 200);
        return new Milestone(MilestoneId.New(), tenantId, projectId, key, title,
            dueAt, createdBy, DateTimeOffset.UtcNow);
    }

    /// <summary>Re-plans the milestone's title and due date. Returns false when nothing changed.</summary>
    public bool Replan(string title, DateTimeOffset? dueAt, int expectedVersion)
    {
        if (expectedVersion != Version)
            throw new WorkItemConcurrencyException(expectedVersion, Version);
        Validate(title, nameof(title), 200);
        if (Title == title && DueAt == dueAt)
            return false;

        Title = title;
        DueAt = dueAt;
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
        return true;
    }

    public MilestoneTransition ChangeState(MilestoneState target, PrincipalId actor,
        string reason, int expectedVersion)
    {
        if (expectedVersion != Version)
            throw new WorkItemConcurrencyException(expectedVersion, Version);
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
            throw new ArgumentException("A transition reason of 1-1000 characters is required.", nameof(reason));
        if (!IsAllowed(State, target))
            throw new InvalidOperationException($"Milestone cannot transition from '{State}' to '{target}'.");

        var previous = State;
        State = target;
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
        return MilestoneTransition.Create(this, previous, target, actor, reason);
    }

    private static bool IsAllowed(MilestoneState from, MilestoneState to) =>
        (from, to) switch
        {
            (MilestoneState.Planned, MilestoneState.InProgress or MilestoneState.Cancelled) => true,
            (MilestoneState.InProgress, MilestoneState.Completed or MilestoneState.Cancelled) => true,
            (MilestoneState.Completed, MilestoneState.InProgress) => true,
            // Re-planning: a cancelled milestone can be brought back into the plan.
            (MilestoneState.Cancelled, MilestoneState.Planned) => true,
            _ => false,
        };

    private static void Validate(string value, string parameter, int max)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > max)
            throw new ArgumentException($"{parameter} is required and must not exceed {max} characters.", parameter);
    }
}
