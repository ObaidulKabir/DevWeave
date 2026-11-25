using DevWeave.Domain.Commercial;
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

    /// <summary>
    /// Prior package this continuation replaces. Null for original packages; never rewritten on the
    /// predecessor itself.
    /// </summary>
    public WorkPackageId? PredecessorWorkPackageId { get; private set; }

    /// <summary>The exact approved knowledge revisions (from the package's baseline) this work implements.</summary>
    public IReadOnlyList<Guid> ScopeRevisionIds { get; private set; } = Array.Empty<Guid>();

    /// <summary>The planning work items this package delivers.</summary>
    public IReadOnlyList<Guid> ScopeWorkItemIds { get; private set; } = Array.Empty<Guid>();

    /// <summary>The specification topics whose binding decisions this work follows.</summary>
    public IReadOnlyList<string> ScopeSpecTopics { get; private set; } = Array.Empty<string>();

    public long Version { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public ExecutionLocation ExecutionLocation { get; }
    public string Provider { get; }

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
        DateTimeOffset updatedAt,
        WorkPackageId? predecessorWorkPackageId = null,
        ExecutionLocation executionLocation = ExecutionLocation.CustomerManaged,
        string provider = ExecutionFacts.CustomerProvider)
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
        PredecessorWorkPackageId = predecessorWorkPackageId;
        ExecutionLocation = executionLocation;
        Provider = ExecutionFacts.RequireProvider(provider);
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
        IReadOnlyList<string>? scopeSpecTopics = null,
        string? executionLocation = null,
        string provider = ExecutionFacts.CustomerProvider)
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
            now,
            executionLocation: ExecutionFacts.ParseLocation(executionLocation),
            provider: provider);
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

    /// <summary>
    /// Creates a replacement package on a new baseline, linked to <paramref name="predecessor"/>. The
    /// predecessor's historical baseline identity is left untouched.
    /// </summary>
    public static WorkPackage CreateContinuation(
        WorkPackage predecessor,
        Guid newBaselineId,
        string continuationKey)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        if (newBaselineId == Guid.Empty)
            throw new ArgumentException("Continuation baseline is required.", nameof(newBaselineId));
        if (string.IsNullOrWhiteSpace(continuationKey))
            throw new ArgumentException("Continuation key is required.", nameof(continuationKey));
        if (string.Equals(continuationKey, predecessor.Key, StringComparison.Ordinal))
            throw new ArgumentException("Continuation key must differ from the predecessor key.");

        var now = DateTimeOffset.UtcNow;
        var package = new WorkPackage(
            WorkPackageId.New(),
            predecessor.TenantId,
            predecessor.ProjectId,
            predecessor.CampaignId,
            newBaselineId,
            continuationKey.Trim(),
            predecessor.Objective,
            WorkPackageStatus.Available,
            1,
            now,
            now,
            predecessor.Id,
            predecessor.ExecutionLocation,
            predecessor.Provider);
        package.ScopeRevisionIds = predecessor.ScopeRevisionIds.ToArray();
        package.ScopeWorkItemIds = predecessor.ScopeWorkItemIds.ToArray();
        package.ScopeSpecTopics = predecessor.ScopeSpecTopics.ToArray();
        return package;
    }
}
