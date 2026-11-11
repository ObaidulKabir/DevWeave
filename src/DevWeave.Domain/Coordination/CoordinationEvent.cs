using DevWeave.Domain.Projects;
using DevWeave.Domain.Identity;

namespace DevWeave.Domain.Coordination;

/// <summary>
/// A high-level coordination event recorded for a campaign (SRS MCP-035).
/// </summary>
public sealed class CoordinationEvent
{
    public Guid Id { get; }
    public TenantId TenantId { get; }
    public ProjectId ProjectId { get; }
    public CampaignId CampaignId { get; }
    public ChannelId? ChannelId { get; }
    public string EventKey { get; }
    public string EventType { get; }
    public string PayloadJson { get; }
    public long Sequence { get; }
    public DateTimeOffset RecordedAt { get; }

    private CoordinationEvent(
        Guid id,
        TenantId tenantId,
        ProjectId projectId,
        CampaignId campaignId,
        ChannelId? channelId,
        string eventKey,
        string eventType,
        string payloadJson,
        long sequence,
        DateTimeOffset recordedAt)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        CampaignId = campaignId;
        ChannelId = channelId;
        EventKey = eventKey;
        EventType = eventType;
        PayloadJson = payloadJson;
        Sequence = sequence;
        RecordedAt = recordedAt;
    }

    public static CoordinationEvent Record(
        TenantId tenantId,
        ProjectId projectId,
        CampaignId campaignId,
        string eventKey,
        string eventType,
        string payloadJson,
        long sequence,
        ChannelId? channelId = null)
    {
        return new CoordinationEvent(
            Guid.NewGuid(),
            tenantId,
            projectId,
            campaignId,
            channelId,
            eventKey,
            eventType,
            payloadJson,
            sequence,
            DateTimeOffset.UtcNow);
    }
}
