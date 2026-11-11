using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Coordination;

public enum KillSwitchTargetType
{
    Runtime,
    AgentPrincipal,
    Campaign,
    Integration,
    Provider
}

/// <summary>
/// A control-plane kill switch that suspends operations for a specific target (SRS MCO-048).
/// </summary>
public sealed class CoordinationKillSwitch
{
    public Guid Id { get; }
    public TenantId TenantId { get; }
    public ProjectId? ProjectId { get; }
    public KillSwitchTargetType TargetType { get; }
    public Guid TargetId { get; }
    public string Reason { get; }
    public PrincipalId CreatedBy { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? ExpiresAt { get; }
    public bool IsActive { get; private set; }

    private CoordinationKillSwitch(
        Guid id,
        TenantId tenantId,
        ProjectId? projectId,
        KillSwitchTargetType targetType,
        Guid targetId,
        string reason,
        PrincipalId createdBy,
        DateTimeOffset createdAt,
        DateTimeOffset? expiresAt,
        bool isActive)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        TargetType = targetType;
        TargetId = targetId;
        Reason = reason;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        IsActive = isActive;
    }

    public static CoordinationKillSwitch Activate(
        TenantId tenantId,
        ProjectId? projectId,
        KillSwitchTargetType targetType,
        Guid targetId,
        string reason,
        PrincipalId createdBy,
        DateTimeOffset? expiresAt = null)
    {
        return new CoordinationKillSwitch(
            Guid.NewGuid(),
            tenantId,
            projectId,
            targetType,
            targetId,
            reason,
            createdBy,
            DateTimeOffset.UtcNow,
            expiresAt,
            true);
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public bool IsEffective()
    {
        if (!IsActive) return false;
        if (ExpiresAt.HasValue && DateTimeOffset.UtcNow > ExpiresAt.Value) return false;
        return true;
    }
}
