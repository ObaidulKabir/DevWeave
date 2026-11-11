using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Coordination;

public enum IntegrationStatus
{
    Queued,
    Processing,
    Accepted,
    Rejected,
    Failed
}

/// <summary>
/// A unit of work submitted for integration (SRS MCO-BR-09).
/// </summary>
public sealed class IntegrationQueueItem
{
    public IntegrationQueueItemId Id { get; }
    public TenantId TenantId { get; }
    public ProjectId ProjectId { get; }
    public ChangeSetId ChangeSetId { get; }
    public string IdempotencyKey { get; }
    public IntegrationStatus Status { get; private set; }
    public string? Outcome { get; private set; }
    public long? Fence { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? DecidedAt { get; private set; }

    private IntegrationQueueItem(
        IntegrationQueueItemId id,
        TenantId tenantId,
        ProjectId projectId,
        ChangeSetId changeSetId,
        string idempotencyKey,
        IntegrationStatus status,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        ChangeSetId = changeSetId;
        IdempotencyKey = idempotencyKey;
        Status = status;
        CreatedAt = createdAt;
    }

    public static IntegrationQueueItem Enqueue(
        TenantId tenantId,
        ProjectId projectId,
        ChangeSetId changeSetId,
        string idempotencyKey)
    {
        return new IntegrationQueueItem(
            IntegrationQueueItemId.New(),
            tenantId,
            projectId,
            changeSetId,
            idempotencyKey,
            IntegrationStatus.Queued,
            DateTimeOffset.UtcNow);
    }

    public void Decide(bool accepted, string outcome, long fence)
    {
        if (Status != IntegrationStatus.Queued && Status != IntegrationStatus.Processing)
            throw new InvalidOperationException("Item already decided.");

        Status = accepted ? IntegrationStatus.Accepted : IntegrationStatus.Rejected;
        Outcome = outcome;
        Fence = fence;
        DecidedAt = DateTimeOffset.UtcNow;
    }

    public void MarkProcessing()
    {
        if (Status != IntegrationStatus.Queued)
            throw new InvalidOperationException("Item not in queue.");

        Status = IntegrationStatus.Processing;
    }
}
