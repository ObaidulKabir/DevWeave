using DevWeave.Application.Abstractions;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Mcp.Protocol;

/// <summary>
/// Maps MCP tools to required agent capabilities so Planning and Execution principals stay separated.
/// </summary>
public static class McpToolAuthorization
{
    private static readonly Dictionary<string, AgentCapability[]> RequiredCapabilities = new(StringComparer.Ordinal)
    {
        ["read_plan_outline"] = [AgentCapability.ReadKnowledge],
        ["propose_plan_expansion"] = [AgentCapability.WriteKnowledge],
        ["propose_initial_plan"] = [AgentCapability.WriteKnowledge],
        ["list_codebase_targets"] = [AgentCapability.ReadKnowledge, AgentCapability.ExecuteWorkflow],
        ["register_codebase_target"] = [AgentCapability.WriteKnowledge],
        ["list_schemas"] = [AgentCapability.ReadKnowledge],
        // Plan-import authors need the active relation vocabulary even when they do not traverse edges.
        ["list_relation_types"] = [AgentCapability.TraverseRelations, AgentCapability.WriteKnowledge],
        ["resolve_spec"] = [AgentCapability.ReadKnowledge],
        ["get_effective_spec"] = [AgentCapability.ReadKnowledge],
        ["list_spec_topics"] = [AgentCapability.ReadKnowledge],
        ["get_spec_catalog"] = [AgentCapability.ReadKnowledge],
        ["list_spec_options"] = [AgentCapability.ReadKnowledge],
        ["next_open_topics"] = [AgentCapability.ReadKnowledge],
        ["get_spec_pack_upgrades"] = [AgentCapability.ReadKnowledge],
        ["propose_spec_profile"] = [AgentCapability.WriteKnowledge],
        ["propose_spec_overlay"] = [AgentCapability.WriteKnowledge],
        ["propose_spec_decision"] = [AgentCapability.WriteKnowledge],

        ["send_project_message"] = [AgentCapability.ExecuteWorkflow],
        ["send_message"] = [AgentCapability.ExecuteWorkflow],
        ["read_messages_after_cursor"] = [AgentCapability.ExecuteWorkflow],
        ["list_messages"] = [AgentCapability.ExecuteWorkflow],
        ["acknowledge_messages"] = [AgentCapability.ExecuteWorkflow],
        ["open_channel"] = [AgentCapability.ExecuteWorkflow],
        ["list_channels"] = [AgentCapability.ExecuteWorkflow],
        ["register_runtime"] = [AgentCapability.ExecuteWorkflow],
        ["heartbeat_lease"] = [AgentCapability.ExecuteWorkflow],
        ["request_assignment"] = [AgentCapability.ExecuteWorkflow],
        ["get_plan_status"] = [AgentCapability.ExecuteWorkflow],
        ["list_work_packages"] = [AgentCapability.ExecuteWorkflow],
        ["get_work_status"] = [AgentCapability.ExecuteWorkflow],
        ["create_work_package"] = [AgentCapability.ExecuteWorkflow],
        ["list_ready_work"] = [AgentCapability.ExecuteWorkflow],
        ["release_lease"] = [AgentCapability.ExecuteWorkflow],
        ["submit_change_set"] = [AgentCapability.ExecuteWorkflow],
        ["report_workthrough"] = [AgentCapability.ExecuteWorkflow],
        ["request_integration"] = [AgentCapability.ExecuteWorkflow],
        ["get_plan_baseline"] = [AgentCapability.ExecuteWorkflow],
    };

    public static bool TryGetRequiredCapabilities(string toolName, out AgentCapability[] capabilities) =>
        RequiredCapabilities.TryGetValue(toolName, out capabilities!);

    public static async Task EnsureCanCallAsync(
        IAgentGrantRepository grants,
        TenantId tenant,
        ProjectId project,
        PrincipalId agent,
        string toolName,
        CancellationToken ct)
    {
        if (!TryGetRequiredCapabilities(toolName, out var required))
            throw new InvalidOperationException($"Unknown tool: {toolName}");

        var now = DateTimeOffset.UtcNow;
        var active = (await grants.ListForAgentAsync(tenant, agent, ct))
            .Where(g => g.IsActive(now) && g.CoversProject(project) && required.Contains(g.Capability))
            .Any();
        if (!active)
            throw new UnauthorizedTenantException(
                $"Tool '{toolName}' requires an active {string.Join(" or ", required)} grant for this project.");
    }

    public static async Task<HashSet<AgentCapability>> LoadActiveCapabilitiesAsync(
        IAgentGrantRepository grants,
        TenantId tenant,
        ProjectId project,
        PrincipalId agent,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        return (await grants.ListForAgentAsync(tenant, agent, ct))
            .Where(g => g.IsActive(now) && g.CoversProject(project))
            .Select(g => g.Capability)
            .ToHashSet();
    }

    public static bool IsAllowed(string toolName, IReadOnlySet<AgentCapability> capabilities) =>
        TryGetRequiredCapabilities(toolName, out var required) && required.Any(capabilities.Contains);
}
