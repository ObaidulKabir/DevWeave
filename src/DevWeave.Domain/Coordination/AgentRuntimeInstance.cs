using DevWeave.Domain.Commercial;
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
    public ExecutionLocation ExecutionLocation { get; }
    public string Provider { get; }
    public string? WorkspaceProvider { get; }
    public string? BillingMeter { get; }

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
        DateTimeOffset createdAt,
        ExecutionLocation executionLocation,
        string provider,
        string? workspaceProvider,
        string? billingMeter)
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
        ExecutionLocation = executionLocation;
        Provider = provider;
        WorkspaceProvider = workspaceProvider;
        BillingMeter = billingMeter;
    }

    public static AgentRuntimeInstance Register(
        TenantId tenantId,
        ProjectId projectId,
        PrincipalId principalId,
        string sessionKey,
        string runtimeClass,
        string protocolVersion,
        string? executionLocation = null,
        string provider = ExecutionFacts.CustomerProvider,
        string? workspaceProvider = null,
        string? billingMeter = null)
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
            DateTimeOffset.UtcNow,
            ExecutionFacts.ParseLocation(executionLocation),
            ExecutionFacts.RequireProvider(provider),
            string.IsNullOrWhiteSpace(workspaceProvider) ? null : workspaceProvider.Trim(),
            string.IsNullOrWhiteSpace(billingMeter) ? null : billingMeter.Trim());
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
