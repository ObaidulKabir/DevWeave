using DevWeave.Application.Abstractions;
using DevWeave.Domain.Activities;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Activities;

/// <summary>
/// Exports a project's provenance timeline as a PROV-O projection (W3C PROV-O mapping) including
/// optional agent execution metadata (provider/model/configuration) when present.
/// </summary>
public sealed class ExportProvenance
{
    private readonly IActivityRepository _activities;
    private readonly IAgentExecutionMetadataRepository _metadata;

    public ExportProvenance(
        IActivityRepository activities,
        IAgentExecutionMetadataRepository metadata)
    {
        _activities = activities;
        _metadata = metadata;
    }

    public async Task<IReadOnlyList<ProvenanceRecord>> ExecuteAsync(
        TenantId tenantId,
        ProjectId projectId,
        CancellationToken ct = default)
    {
        var activities = await _activities.ListForProjectAsync(tenantId, projectId, ct);
        var metadata = await _metadata.ListForProjectActivitiesAsync(
            tenantId,
            activities.Select(a => a.Id).ToArray(),
            ct);
        var byActivity = metadata.ToDictionary(m => m.ActivityId);

        return activities.Select(activity => ToRecord(
            activity,
            byActivity.TryGetValue(activity.Id, out var meta) ? meta : null)).ToList();
    }

    public static ProvenanceRecord ToRecord(
        Activity activity,
        Domain.Agents.AgentExecutionMetadata? metadata = null) =>
        new(
            activity.Id.Value,
            activity.Type.ToString(),
            activity.Actor.Value,
            activity.Inputs
                .Select(i => new ProvenanceRef(i.ItemId.Value, i.RevisionId.Value))
                .ToList(),
            activity.Outputs
                .Select(o => new ProvenanceRef(o.ItemId.Value, o.RevisionId.Value))
                .ToList(),
            metadata is null
                ? null
                : new AgentExecutionProvenance(
                    metadata.Provider,
                    metadata.Model,
                    metadata.ModelVersion,
                    metadata.ConfigurationFingerprint,
                    metadata.ToolFingerprint,
                    metadata.ExternalRunReference,
                    metadata.AuthorizationContext));
}
