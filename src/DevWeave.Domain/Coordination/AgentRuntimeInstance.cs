using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Coordination;

public enum RuntimeStatus
{
    Connected,
    Disconnected,
    Suspended
}

public sealed class AgentRuntimeInstance
{
    public RuntimeId Id { get; }
    public TenantId TenantId { get; }
    public ProjectId ProjectId { get; }
    public PrincipalId PrincipalId { get; }
    public string SessionKey { get; }
    public string RuntimeClass { get; }
    public string ProtocolVersion { get; }
    public RuntimeStatus Status { get; private set; }
    public DateTimeOffset? LastHeartbeatAt { get; private set; }
    public DateTimeOffset CreatedAt { get; }

    private AgentRuntimeInstance(
        RuntimeId id,
        TenantId tenantId,
        ProjectId projectId,
        PrincipalId principalId,
        string sessionKey,
        string runtimeClass,
        string protocolVersion,
        RuntimeStatus status,
        DateTimeOffset? lastHeartbeatAt,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        PrincipalId = principalId;
        SessionKey = sessionKey;
        RuntimeClass = runtimeClass;
        ProtocolVersion = protocolVersion;
        Status = status;
        LastHeartbeatAt = lastHeartbeatAt;
        CreatedAt = createdAt;
    }

    public static AgentRuntimeInstance Register(
        TenantId tenantId,
        ProjectId projectId,
        PrincipalId principalId,
        string sessionKey,
        string runtimeClass,
        string protocolVersion)
    {
        return new AgentRuntimeInstance(
            RuntimeId.New(),
            tenantId,
            projectId,
            principalId,
            sessionKey,
            runtimeClass,
            protocolVersion,
            RuntimeStatus.Connected,
            null,
            DateTimeOffset.UtcNow);
    }

    public void Heartbeat(DateTimeOffset observedAt)
    {
        LastHeartbeatAt = observedAt;
    }

    public void Disconnect()
    {
        Status = RuntimeStatus.Disconnected;
    }
}
