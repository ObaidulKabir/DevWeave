using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Coordination;

/// <summary>
/// The codebase commit a campaign was created against. Copied at creation so a later
/// registry edit cannot move the campaign (R1-A).
/// </summary>
public sealed class CampaignCodebasePin
{
    public CampaignId CampaignId { get; }
    public CodebaseTargetId TargetId { get; }
    public TenantId TenantId { get; }
    public ProjectId ProjectId { get; }
    public string Provider { get; }
    public string RepositoryId { get; }
    public string TargetBranch { get; }
    public string BaseCommitSha { get; }
    public DateTimeOffset PinnedAt { get; }

    private CampaignCodebasePin(
        CampaignId campaignId,
        CodebaseTargetId targetId,
        TenantId tenantId,
        ProjectId projectId,
        string provider,
        string repositoryId,
        string targetBranch,
        string baseCommitSha,
        DateTimeOffset pinnedAt)
    {
        CampaignId = campaignId;
        TargetId = targetId;
        TenantId = tenantId;
        ProjectId = projectId;
        Provider = provider;
        RepositoryId = repositoryId;
        TargetBranch = targetBranch;
        BaseCommitSha = baseCommitSha;
        PinnedAt = pinnedAt;
    }

    public static CampaignCodebasePin Pin(CampaignId campaignId, CodebaseTarget target)
    {
        return new CampaignCodebasePin(
            campaignId,
            target.Id,
            target.TenantId,
            target.ProjectId,
            target.Provider,
            target.RepositoryId,
            target.TargetBranch,
            target.BaseCommitSha,
            DateTimeOffset.UtcNow);
    }
}
