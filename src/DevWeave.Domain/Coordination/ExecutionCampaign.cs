using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Coordination;

public enum CampaignStatus
{
    Proposed,
    Active,
    Suspended,
    Completed,
    Failed
}

public sealed class ExecutionCampaign
{
    public CampaignId Id { get; }
    public TenantId TenantId { get; }
    public ProjectId ProjectId { get; }
    public Guid BaselineId { get; } // Planned baseline
    public string Objective { get; }
    public string ExecutionMode { get; } // "Autonomous", "Mediated", "HumanInLoop"
    public CampaignStatus Status { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private ExecutionCampaign(
        CampaignId id,
        TenantId tenantId,
        ProjectId projectId,
        Guid baselineId,
        string objective,
        string executionMode,
        CampaignStatus status,
        long version,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        BaselineId = baselineId;
        Objective = objective;
        ExecutionMode = executionMode;
        Status = status;
        Version = version;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static ExecutionCampaign Create(
        TenantId tenantId,
        ProjectId projectId,
        Guid baselineId,
        string objective,
        string executionMode)
    {
        var now = DateTimeOffset.UtcNow;
        return new ExecutionCampaign(
            CampaignId.New(),
            tenantId,
            projectId,
            baselineId,
            objective,
            executionMode,
            CampaignStatus.Proposed,
            1,
            now,
            now);
    }

    public void Activate()
    {
        Status = CampaignStatus.Active;
        UpdatedAt = DateTimeOffset.UtcNow;
        Version++;
    }

    public void Suspend()
    {
        Status = CampaignStatus.Suspended;
        UpdatedAt = DateTimeOffset.UtcNow;
        Version++;
    }
}
