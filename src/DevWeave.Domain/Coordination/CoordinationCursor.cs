using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Coordination;

/// <summary>
/// Tracks the progress of a principal (agent or human) within a coordination channel (SRS MCP-035).
/// </summary>
public sealed class CoordinationCursor
{
    public TenantId TenantId { get; }
    public ProjectId ProjectId { get; }
    public ChannelId ChannelId { get; }
    public PrincipalId PrincipalId { get; }
    public long LastReadSequence { get; private set; }
    public long LastAcknowledgedSequence { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private CoordinationCursor(
        TenantId tenantId,
        ProjectId projectId,
        ChannelId channelId,
        PrincipalId principalId,
        long lastReadSequence,
        long lastAcknowledgedSequence,
        DateTimeOffset updatedAt)
    {
        TenantId = tenantId;
        ProjectId = projectId;
        ChannelId = channelId;
        PrincipalId = principalId;
        LastReadSequence = lastReadSequence;
        LastAcknowledgedSequence = lastAcknowledgedSequence;
        UpdatedAt = updatedAt;
    }

    public static CoordinationCursor Create(
        TenantId tenantId,
        ProjectId projectId,
        ChannelId channelId,
        PrincipalId principalId)
    {
        return new CoordinationCursor(
            tenantId,
            projectId,
            channelId,
            principalId,
            0,
            0,
            DateTimeOffset.UtcNow);
    }

    public void UpdateRead(long sequence)
    {
        if (sequence > LastReadSequence)
        {
            LastReadSequence = sequence;
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    public void UpdateAcknowledged(long sequence)
    {
        if (sequence > LastAcknowledgedSequence)
        {
            LastAcknowledgedSequence = sequence;
            UpdateRead(sequence);
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}
