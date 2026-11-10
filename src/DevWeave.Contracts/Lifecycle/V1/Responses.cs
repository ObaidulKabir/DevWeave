namespace DevWeave.Contracts.Lifecycle.V1;

public sealed record LifecycleProfileResponse(
    Guid Id,
    Guid TenantId,
    string Key,
    string Name,
    string Version,
    DateTimeOffset CreatedAt);