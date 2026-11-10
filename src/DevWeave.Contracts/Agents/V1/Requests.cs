using DevWeave.Domain.Activities;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Knowledge;

namespace DevWeave.Contracts.Agents.V1;

public sealed record RegisterAgentRequest(string Email);

public sealed record IssueAgentCredentialRequest(string Label, DateTimeOffset? ExpiresAt);

public sealed record AuthenticateAgentRequest(Guid CredentialId, string Secret);

public sealed record RevokeAgentCredentialRequest(Guid Actor);

public sealed record DisableAgentCredentialRequest(Guid Actor);

public sealed record EnableAgentCredentialRequest(Guid Actor);

public sealed record RotateAgentCredentialRequest(Guid Actor, DateTimeOffset? ExpiresAt);

public sealed record GrantAgentCapabilityRequest(
    Guid AgentId,
    Guid? ProjectId,
    AgentCapability Capability,
    DateTimeOffset? ExpiresAt,
    Classification ClassificationCeiling = Classification.Internal);

public sealed record ApproveAgentGrantRequest(Guid Approver);

public sealed record RevokeAgentGrantRequest(Guid Revoker);

public sealed record AssembleContextPackageRequest(
    string TaskKey,
    string IdempotencyKey,
    IReadOnlyList<Guid> ItemIds);

public sealed record RecordAgentExecutionRequest(
    Guid AgentId,
    Guid ContextPackageId,
    string Purpose,
    ActivityStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt);