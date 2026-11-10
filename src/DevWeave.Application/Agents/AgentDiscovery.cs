using DevWeave.Domain.Agents;
using DevWeave.Domain.Relations;
using DevWeave.Domain.Schema;

namespace DevWeave.Application.Agents;

/// <summary>
/// The authorized discovery surface returned for an AI agent (SRS AGT-003). It combines the agent's
/// effective (approved and unexpired) capability grants with the concrete permitted resources those
/// grants unlock: the published schemas the agent may reference, the active relation types it may
/// traverse, and the tool identifiers it may invoke. An agent uses this snapshot to learn what it may
/// do and where without probing for facts it is not entitled to see.
/// </summary>
public sealed record AgentDiscovery(
    IReadOnlyList<AgentGrant> Grants,
    IReadOnlyList<SchemaDefinition> PermittedSchemas,
    IReadOnlyList<RelationType> PermittedRelationTypes,
    IReadOnlyList<AgentCapability> PermittedTools);