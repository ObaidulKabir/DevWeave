using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Coordination;

public enum WorkPackageStatus
{
    Proposed,
    Available,
    Leased,
    Submitted,
    Integrated,
    Failed,
    Cancelled
}

public sealed class WorkPackage
{
    public WorkPackageId Id { get; }
    public TenantId TenantId { get; }
    public ProjectId ProjectId { get; }
    public CampaignId CampaignId { get; }
    public Guid BaselineId { get; }
    public string Key { get; }
    public string Objective { get; }
    public WorkPackageStatus Status { get; private set; }

    /// <summary>The exact approved knowledge revisions (from the package's baseline) this work implements.</summary>
    public IReadOnlyList<Guid> ScopeRevisionIds { get; private set; } = Array.Empty<Guid>();

    /// <summary>The planning work items this package delivers.</summary>
    public IReadOnlyList<Guid> ScopeWorkItemIds { get; private set; } = Array.Empty<Guid>();

    /// <summary>The specification topics whose binding decisions this work follows.</summary>
    public IReadOnlyList<string> ScopeSpecTopics { get; private set; } = Array.Empty<string>();

    public long Version { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private WorkPackage(
        WorkPackageId id,
        TenantId tenantId,
        ProjectId projectId,
        CampaignId campaignId,
        Guid baselineId,
        string key,
        string objective,
        WorkPackageStatus status,
        long version,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        CampaignId = campaignId;
        BaselineId = baselineId;
        Key = key;
        Objective = objective;
        Status = status;
        Version = version;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    /// <summary>True when nothing is declared, so a plan change cannot be traced to this package.</summary>
    public bool IsUnscoped => ScopeRevisionIds.Count == 0 && ScopeWorkItemIds.Count == 0 && ScopeSpecTopics.Count == 0;

    /// <summary>True while work on the package can still progress (lease, submit, integrate).</summary>
    public bool IsOpen => Status is WorkPackageStatus.Proposed or WorkPackageStatus.Available
        or WorkPackageStatus.Leased or WorkPackageStatus.Submitted;

    public static WorkPackage Create(
        TenantId tenantId,
        ProjectId projectId,
        CampaignId campaignId,
        Guid baselineId,
        string key,
        string objective,
        IReadOnlyList<Guid>? scopeRevisionIds = null,
        IReadOnlyList<Guid>? scopeWorkItemIds = null,
        IReadOnlyList<string>? scopeSpecTopics = null)
    {
        var revisions = (scopeRevisionIds ?? Array.Empty<Guid>()).ToArray();
        var workItems = (scopeWorkItemIds ?? Array.Empty<Guid>()).ToArray();
        var topics = (scopeSpecTopics ?? Array.Empty<string>()).Select(t => t.Trim()).ToArray();
        if (revisions.Distinct().Count() != revisions.Length ||
            workItems.Distinct().Count() != workItems.Length ||
            topics.Distinct(StringComparer.Ordinal).Count() != topics.Length)
            throw new ArgumentException("A work package scope cannot list the same reference twice.");
        if (revisions.Contains(Guid.Empty) || workItems.Contains(Guid.Empty) || topics.Any(string.IsNullOrEmpty))
            throw new ArgumentException("Work package scope references must not be empty.");

        var now = DateTimeOffset.UtcNow;
        var package = new WorkPackage(
            WorkPackageId.New(),
            tenantId,
            projectId,
            campaignId,
            baselineId,
            key,
            objective,
            WorkPackageStatus.Proposed,
            1,
            now,
            now);
        package.ScopeRevisionIds = revisions;
        package.ScopeWorkItemIds = workItems;
        package.ScopeSpecTopics = topics;
        return package;
    }

    /// <summary>Stops the package for good; leases, submissions and integration are then refused.</summary>
    public void Cancel()
    {
        if (!IsOpen)
            throw new InvalidOperationException($"Work package is already {Status}.");
        Status = WorkPackageStatus.Cancelled;
        UpdatedAt = DateTimeOffset.UtcNow;
        Version++;
    }

    public void MakeAvailable()
    {
        Status = WorkPackageStatus.Available;
        UpdatedAt = DateTimeOffset.UtcNow;
        Version++;
    }

    public void MarkLeased()
    {
        Status = WorkPackageStatus.Leased;
        UpdatedAt = DateTimeOffset.UtcNow;
        Version++;
    }
}
