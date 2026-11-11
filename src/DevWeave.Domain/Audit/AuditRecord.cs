using DevWeave.Domain.Identity;

namespace DevWeave.Domain.Audit;

/// <summary>
/// A tenant-scoped, immutable record of a security-sensitive or substantive state change (SRS AUD-01,
/// AUD-02, AUD-03). Unlike <see cref="DevWeave.Domain.Activities.Activity"/> which tracks project-specific 
/// knowledge work, audit records track management and administrative operations (logins, membership, grants).
/// </summary>
public sealed class AuditRecord
{
    public AuditRecordId Id { get; }
    public TenantId TenantId { get; }
    public DateTimeOffset OccurredAt { get; }
    public PrincipalId ActorId { get; }
    public string Action { get; }
    public string ResourceType { get; }
    public string ResourceId { get; }
    public string? MetadataJson { get; }
    public string? CorrelationId { get; }

    private AuditRecord(
        AuditRecordId id,
        TenantId tenantId,
        DateTimeOffset occurredAt,
        PrincipalId actorId,
        string action,
        string resourceType,
        string resourceId,
        string? metadataJson,
        string? correlationId)
    {
        Id = id;
        TenantId = tenantId;
        OccurredAt = occurredAt;
        ActorId = actorId;
        Action = action;
        ResourceType = resourceType;
        ResourceId = resourceId;
        MetadataJson = metadataJson;
        CorrelationId = correlationId;
    }

    public static AuditRecord Record(
        TenantId tenantId,
        PrincipalId actorId,
        string action,
        string resourceType,
        string resourceId,
        string? metadataJson = null,
        string? correlationId = null)
    {
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("Audit action is required.", nameof(action));

        if (string.IsNullOrWhiteSpace(resourceType))
            throw new ArgumentException("Resource type is required.", nameof(resourceType));

        if (string.IsNullOrWhiteSpace(resourceId))
            throw new ArgumentException("Resource identifier is required.", nameof(resourceId));

        return new AuditRecord(
            AuditRecordId.New(),
            tenantId,
            DateTimeOffset.UtcNow,
            actorId,
            action,
            resourceType,
            resourceId,
            metadataJson,
            correlationId);
    }
}
