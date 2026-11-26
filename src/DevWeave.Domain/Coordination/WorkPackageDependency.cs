using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Coordination;

/// <summary>
/// One edge in a campaign's work-package DAG. Both packages stay on the same baseline (R1-B).
/// </summary>
public sealed class WorkPackageDependency
{
    public WorkPackageId PackageId { get; }
    public WorkPackageId DependsOnPackageId { get; }
    public TenantId TenantId { get; }
    public ProjectId ProjectId { get; }
    public CampaignId CampaignId { get; }
    public Guid BaselineId { get; }

    private WorkPackageDependency(
        WorkPackageId packageId,
        WorkPackageId dependsOnPackageId,
        TenantId tenantId,
        ProjectId projectId,
        CampaignId campaignId,
        Guid baselineId)
    {
        PackageId = packageId;
        DependsOnPackageId = dependsOnPackageId;
        TenantId = tenantId;
        ProjectId = projectId;
        CampaignId = campaignId;
        BaselineId = baselineId;
    }

    public static WorkPackageDependency Link(WorkPackage package, WorkPackage predecessor)
    {
        return new WorkPackageDependency(
            package.Id,
            predecessor.Id,
            package.TenantId,
            package.ProjectId,
            package.CampaignId,
            package.BaselineId);
    }
}
