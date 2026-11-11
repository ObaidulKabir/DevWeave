using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Coordination;

namespace DevWeave.Domain.Commercial;

/// <summary>
/// A record of a specific usage event (meter) that can be attributed to a tenant, project, 
/// campaign or runtime (SRS COM-003, COM-021).
/// </summary>
public sealed class UsageObservation
{
    public UsageObservationId Id { get; }
    public TenantId TenantId { get; }
    public ProjectId? ProjectId { get; }
    public CampaignId? CampaignId { get; }
    public RuntimeId? RuntimeId { get; }
    public string ExecutionMode { get; }
    public string ResourceClass { get; }
    public string Meter { get; }
    public decimal Quantity { get; }
    public DateTimeOffset StartedAt { get; }
    public DateTimeOffset EndedAt { get; }
    public string IdempotencyKey { get; }

    private UsageObservation(
        UsageObservationId id,
        TenantId tenantId,
        ProjectId? projectId,
        CampaignId? campaignId,
        RuntimeId? runtimeId,
        string executionMode,
        string resourceClass,
        string meter,
        decimal quantity,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        string idempotencyKey)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        CampaignId = campaignId;
        RuntimeId = runtimeId;
        ExecutionMode = executionMode;
        ResourceClass = resourceClass;
        Meter = meter;
        Quantity = quantity;
        StartedAt = startedAt;
        EndedAt = endedAt;
        IdempotencyKey = idempotencyKey;
    }

    public static UsageObservation Record(
        TenantId tenantId,
        ProjectId? projectId,
        CampaignId? campaignId,
        RuntimeId? runtimeId,
        string executionMode,
        string resourceClass,
        string meter,
        decimal quantity,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        string idempotencyKey)
    {
        return new UsageObservation(
            UsageObservationId.New(),
            tenantId,
            projectId,
            campaignId,
            runtimeId,
            executionMode,
            resourceClass,
            meter,
            quantity,
            startedAt,
            endedAt,
            idempotencyKey);
    }
}
