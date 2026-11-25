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
    public long LastNotifiedSequence { get; private set; }
    public long LastReadSequence { get; private set; }
    public long LastAcknowledgedSequence { get; private set; }
    public long LastAcceptedSequence { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private CoordinationCursor(
        TenantId tenantId,
        ProjectId projectId,
        ChannelId channelId,
        PrincipalId principalId,
        long lastNotifiedSequence,
        long lastReadSequence,
        long lastAcknowledgedSequence,
        long lastAcceptedSequence,
        DateTimeOffset updatedAt)
    {
        TenantId = tenantId;
        ProjectId = projectId;
        ChannelId = channelId;
        PrincipalId = principalId;
        LastNotifiedSequence = lastNotifiedSequence;
        LastReadSequence = lastReadSequence;
        LastAcknowledgedSequence = lastAcknowledgedSequence;
        LastAcceptedSequence = lastAcceptedSequence;
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
            0,
            0,
            DateTimeOffset.UtcNow);
    }

    public void UpdateNotified(long sequence) => Advance(sequence, LastNotifiedSequence, value => LastNotifiedSequence = value);

    public void UpdateRead(long sequence) => Advance(sequence, LastReadSequence, value => LastReadSequence = value);

    public void UpdateAcknowledged(long sequence) => Advance(sequence, LastAcknowledgedSequence, value => LastAcknowledgedSequence = value);

    public void UpdateAccepted(long sequence) => Advance(sequence, LastAcceptedSequence, value => LastAcceptedSequence = value);

    private void Advance(long sequence, long current, Action<long> assign)
    {
        if (sequence < 1 || sequence <= current)
            return;
        assign(sequence);
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
