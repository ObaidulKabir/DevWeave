using DevWeave.Application.Abstractions;
using DevWeave.Domain.Activities;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Activities;

/// <summary>
/// Exports a project's provenance timeline as a small PROV-O projection (W3C PROV-O mapping). The
/// mapping is pure C# over the repository port and does not touch any serialization schema.
/// </summary>
public sealed class ExportProvenance
{
    private readonly IActivityRepository _activities;

    public ExportProvenance(IActivityRepository activities)
    {
        _activities = activities;
    }

    public async Task<IReadOnlyList<ProvenanceRecord>> ExecuteAsync(
        TenantId tenantId,
        ProjectId projectId,
        CancellationToken ct = default)
    {
        var activities = await _activities.ListForProjectAsync(tenantId, projectId, ct);

        return activities.Select(ToRecord).ToList();
    }

    public static ProvenanceRecord ToRecord(Activity activity) =>
        new(
            activity.Id.Value,                 // prov:Activity
            activity.Type.ToString(),          // prov:Activity type discriminator
            activity.Actor.Value,              // prov:Agent
            activity.Inputs                     // prov:used
                .Select(i => new ProvenanceRef(i.ItemId.Value, i.RevisionId.Value))
                .ToList(),
            activity.Outputs                    // prov:wasGeneratedBy
                .Select(o => new ProvenanceRef(o.ItemId.Value, o.RevisionId.Value))
                .ToList());
}