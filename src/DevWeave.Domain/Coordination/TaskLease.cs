using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Coordination;

public enum LeaseStatus
{
    Active,
    Released,
    Expired,
    Revoked
}

public sealed class TaskLease
{
    public LeaseId Id { get; }
    public TenantId TenantId { get; }
    public ProjectId ProjectId { get; }
    public WorkPackageId WorkPackageId { get; }
    public RuntimeId RuntimeId { get; }
    public string ResourceScope { get; }
    public long Fence { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public LeaseStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; }

    private TaskLease(
        LeaseId id,
        TenantId tenantId,
        ProjectId projectId,
        WorkPackageId workPackageId,
        RuntimeId runtimeId,
        string resourceScope,
        long fence,
        DateTimeOffset expiresAt,
        LeaseStatus status,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        WorkPackageId = workPackageId;
        RuntimeId = runtimeId;
        ResourceScope = resourceScope;
        Fence = fence;
        ExpiresAt = expiresAt;
        Status = status;
        CreatedAt = createdAt;
    }

    public static TaskLease Acquire(
        TenantId tenantId,
        ProjectId projectId,
        WorkPackageId workPackageId,
        RuntimeId runtimeId,
        string resourceScope,
        long fence,
        TimeSpan duration)
    {
        return new TaskLease(
            LeaseId.New(),
            tenantId,
            projectId,
            workPackageId,
            runtimeId,
            resourceScope,
            fence,
            DateTimeOffset.UtcNow.Add(duration),
            LeaseStatus.Active,
            DateTimeOffset.UtcNow);
    }

    public void Renew(long expectedFence, DateTimeOffset newExpiry)
    {
        if (Fence != expectedFence)
            throw new InvalidOperationException("Fencing token mismatch.");

        if (Status != LeaseStatus.Active)
            throw new InvalidOperationException("Lease is not active.");

        ExpiresAt = newExpiry;
    }

    public void Release(long expectedFence)
    {
        if (Fence != expectedFence)
            throw new InvalidOperationException("Fencing token mismatch.");

        Status = LeaseStatus.Released;
    }
}
