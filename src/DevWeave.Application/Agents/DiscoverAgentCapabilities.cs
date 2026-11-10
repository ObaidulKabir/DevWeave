using DevWeave.Application.Abstractions;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Relations;
using DevWeave.Domain.Schema;

namespace DevWeave.Application.Agents;

/// <summary>
/// Returns the effective (approved and unexpired) capability grants for an AI agent together with the
/// permitted resources those grants unlock (SRS AGT-003). This is the authorized discovery surface an
/// agent uses to learn what it may do and where without probing for facts it is not entitled to see.
/// </summary>
public sealed class DiscoverAgentCapabilities
{
    private readonly IAgentGrantRepository _grants;
    private readonly IPrincipalRepository _principals;
    private readonly ISchemaDefinitionRepository _schemas;
    private readonly IRelationTypeRepository _relationTypes;

    public DiscoverAgentCapabilities(
        IAgentGrantRepository grants,
        IPrincipalRepository principals,
        ISchemaDefinitionRepository schemas,
        IRelationTypeRepository relationTypes)
    {
        _grants = grants;
        _principals = principals;
        _schemas = schemas;
        _relationTypes = relationTypes;
    }

    public async Task<AgentDiscovery> ExecuteAsync(
        TenantId tenantId,
        PrincipalId agentId,
        CancellationToken ct = default)
    {
        _ = await _principals.GetByIdAsync(tenantId, agentId, ct)
            ?? throw new NotFoundException($"Principal '{agentId}' was not found within tenant '{tenantId}'.");

        var grants = await _grants.ListForAgentAsync(tenantId, agentId, ct);
        var effective = grants.Where(grant => grant.IsActive(DateTimeOffset.UtcNow)).ToList();

        // Permitted schemas: knowledge read/write/assembly grants unlock the published schema vocabulary
        // the agent may reference. Schemas are platform-global configuration, so only the published set
        // is discoverable (KNW-008 / SCH-003).
        var hasKnowledgeAccess = effective.Any(g =>
            g.Capability is AgentCapability.ReadKnowledge
                or AgentCapability.WriteKnowledge
                or AgentCapability.AssembleContext);

        var permittedSchemas = hasKnowledgeAccess
            ? (await _schemas.ListAsync(ct)).Where(s => s.IsActive).ToList()
            : new List<SchemaDefinition>();

        // Permitted relation types: only a traversal grant unlocks the active relation-type vocabulary.
        var hasTraverseAccess = effective.Any(g => g.Capability == AgentCapability.TraverseRelations);

        var permittedRelationTypes = hasTraverseAccess
            ? (await _relationTypes.ListActiveAsync(ct)).ToList()
            : new List<RelationType>();

        // Permitted tools are the distinct capabilities the agent holds; each maps to an invocable tool.
        var permittedTools = effective
            .Select(g => g.Capability)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        return new AgentDiscovery(effective, permittedSchemas, permittedRelationTypes, permittedTools);
    }
}