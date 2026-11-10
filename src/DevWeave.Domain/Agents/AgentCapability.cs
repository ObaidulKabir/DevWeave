namespace DevWeave.Domain.Agents;

/// <summary>
/// A fine-grained capability that can be granted to an AI agent principal within a tenant, optionally
/// scoped to a single project (SRS AGT-002, AGT-003). Capabilities are narrower than a role and are
/// discoverable so an agent can determine, without error, what it is allowed to do and where.
/// </summary>
public enum AgentCapability
{
    ReadKnowledge = 1,
    WriteKnowledge = 2,
    AssembleContext = 3,
    TraverseRelations = 4,
    ExecuteWorkflow = 5,
}