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

    public static WorkPackage Create(
        TenantId tenantId,
        ProjectId projectId,
        CampaignId campaignId,
        Guid baselineId,
        string key,
        string objective)
    {
        var now = DateTimeOffset.UtcNow;
        return new WorkPackage(
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
