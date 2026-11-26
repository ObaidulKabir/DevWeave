using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Coordination;

/// <summary>
/// A runtime authorized for a campaign, given one package on that campaign's baseline.
/// The snapshot id and revision list are copied at assignment and are not rewritten (R1-B).
/// </summary>
public sealed class WorkPackageAssignment
{
    public WorkPackageId PackageId { get; }
    public RuntimeId RuntimeId { get; }
    public TenantId TenantId { get; }
    public ProjectId ProjectId { get; }
    public CampaignId CampaignId { get; }
    public Guid BaselineId { get; }
    public Guid ContextSnapshotId { get; }
    public IReadOnlyList<Guid> SnapshotRevisionIds { get; }
    public DateTimeOffset AssignedAt { get; }

    private WorkPackageAssignment(
        WorkPackageId packageId,
        RuntimeId runtimeId,
        TenantId tenantId,
        ProjectId projectId,
        CampaignId campaignId,
        Guid baselineId,
        Guid contextSnapshotId,
        IReadOnlyList<Guid> snapshotRevisionIds,
        DateTimeOffset assignedAt)
    {
        PackageId = packageId;
        RuntimeId = runtimeId;
        TenantId = tenantId;
        ProjectId = projectId;
        CampaignId = campaignId;
        BaselineId = baselineId;
        ContextSnapshotId = contextSnapshotId;
        SnapshotRevisionIds = snapshotRevisionIds;
        AssignedAt = assignedAt;
    }

    public static WorkPackageAssignment Assign(WorkPackage package, RuntimeId runtimeId)
    {
        return new WorkPackageAssignment(
            package.Id,
            runtimeId,
            package.TenantId,
            package.ProjectId,
            package.CampaignId,
            package.BaselineId,
            Guid.NewGuid(),
            package.ScopeRevisionIds.ToArray(),
            DateTimeOffset.UtcNow);
    }
}
