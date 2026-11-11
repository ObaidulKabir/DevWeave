using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Coordination;

/// <summary>
/// A project-scoped communication channel for agent-to-agent or human-to-agent coordination
/// (SRS MCP-001, MCP-017).
/// </summary>
public sealed class CoordinationChannel
{
    public ChannelId Id { get; }
    public TenantId TenantId { get; }
    public ProjectId ProjectId { get; }
    public CampaignId? CampaignId { get; }
    public WorkPackageId? WorkPackageId { get; }
    public string ChannelType { get; } // "Campaign", "WorkPackage", "Direct"
    public string Visibility { get; } // "Public", "Internal", "Restricted"
    public long SequenceHighWatermark { get; private set; }
    public DateTimeOffset CreatedAt { get; }

    private CoordinationChannel(
        ChannelId id,
        TenantId tenantId,
        ProjectId projectId,
        CampaignId? campaignId,
        WorkPackageId? workPackageId,
        string channelType,
        string visibility,
        long sequenceHighWatermark,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        CampaignId = campaignId;
        WorkPackageId = workPackageId;
        ChannelType = channelType;
        Visibility = visibility;
        SequenceHighWatermark = sequenceHighWatermark;
        CreatedAt = createdAt;
    }

    public static CoordinationChannel Open(
        TenantId tenantId,
        ProjectId projectId,
        string channelType,
        string visibility,
        CampaignId? campaignId = null,
        WorkPackageId? workPackageId = null)
    {
        return new CoordinationChannel(
            ChannelId.New(),
            tenantId,
            projectId,
            campaignId,
            workPackageId,
            channelType,
            visibility,
            0,
            DateTimeOffset.UtcNow);
    }

    public long NextSequence()
    {
        return ++SequenceHighWatermark;
    }
}
