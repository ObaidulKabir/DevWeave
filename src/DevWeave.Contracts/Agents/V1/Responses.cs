using DevWeave.Domain.Activities;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;

namespace DevWeave.Contracts.Agents.V1;

public sealed record AgentResponse(
    Guid Id,
    Guid TenantId,
    string Email,
    PrincipalType Type,
    bool IsActive);

/// <summary>The issued credential plus the plaintext secret returned exactly once (AGT-002).</summary>
public sealed record AgentCredentialIssuedResponse(
    Guid Id,
    Guid TenantId,
    Guid AgentId,
    string Label,
    AgentCredentialStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    string PlainTextSecret);

/// <summary>A credential view without the secret, returned by list and lifecycle operations (AGT-002).</summary>
public sealed record AgentCredentialResponse(
    Guid Id,
    Guid TenantId,
    Guid AgentId,
    string Label,
    AgentCredentialStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? RevokedAt);

public sealed record AgentGrantResponse(
    Guid Id,
    Guid TenantId,
    Guid? ProjectId,
    Guid AgentId,
    AgentCapability Capability,
    Classification ClassificationCeiling,
    AgentGrantStatus Status,
    DateTimeOffset GrantedAt,
    DateTimeOffset? ExpiresAt,
    Guid? ApprovedBy,
    DateTimeOffset? ApprovedAt,
    Guid? RevokedBy,
    DateTimeOffset? RevokedAt);

/// <summary>A published schema the agent may reference, surfaced through capability discovery (AGT-003).</summary>
public sealed record SchemaDiscoveryResponse(
    Guid Id,
    string Namespace,
    string Version,
    string Description);

/// <summary>An active relation type the agent may traverse, surfaced through capability discovery (AGT-003).</summary>
public sealed record RelationTypeDiscoveryResponse(
    Guid Id,
    string Code,
    string Name);

/// <summary>
/// The authorized discovery surface returned for an AI agent (AGT-003): effective capability grants plus
/// the permitted schemas, relation types and tools those grants unlock.
/// </summary>
public sealed record AgentDiscoveryResponse(
    IReadOnlyList<AgentGrantResponse> Grants,
    IReadOnlyList<SchemaDiscoveryResponse> PermittedSchemas,
    IReadOnlyList<RelationTypeDiscoveryResponse> PermittedRelationTypes,
    IReadOnlyList<string> PermittedTools);

public sealed record ContextPackageEntryResponse(
    Guid ItemId,
    Guid RevisionId,
    int Position,
    RepresentationKind Representation);

public sealed record ContextPackageResponse(
    Guid Id,
    Guid TenantId,
    Guid ProjectId,
    string TaskKey,
    Guid AgentId,
    string? IdempotencyKey,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ContextPackageEntryResponse> Entries);

/// <summary>The append-only activity created when an agent execution is recorded (AGT-005).</summary>
public sealed record AgentExecutionResponse(
    Guid ActivityId,
    Guid ContextPackageId,
    ActivityStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt);

/// <summary>A structured agent error body with a machine-readable code (mapped to 403/409).</summary>
public sealed record AgentError(string Code, string Message);

/// <summary>A short-lived bearer access token returned by credential-to-token exchange (TEN-007).</summary>
public sealed record AccessTokenResponse(
    string AccessToken,
    string TokenType,
    int ExpiresInSeconds);