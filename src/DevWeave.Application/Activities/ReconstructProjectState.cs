using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Activities;

/// <summary>
/// Reconstructs a project's state purely from its append-only revision and activity history (SRS
/// REV-007). With no timestamp it uses each item's latest revision; with a timestamp it rolls each
/// item back to the greatest revision recorded on or before that moment. Reads are tenant-scoped and
/// return empty results for cross-tenant projects (deny-by-default).
/// </summary>
public sealed class ReconstructProjectState
{
    private readonly IActivityRepository _activities;
    private readonly IKnowledgeItemRepository _items;
    private readonly IKnowledgeRevisionRepository _revisions;

    public ReconstructProjectState(
        IActivityRepository activities,
        IKnowledgeItemRepository items,
        IKnowledgeRevisionRepository revisions)
    {
        _activities = activities;
        _items = items;
        _revisions = revisions;
    }

    public async Task<ProjectStateReconstruction> ExecuteAsync(
        TenantId tenantId,
        ProjectId projectId,
        DateTimeOffset? asOf = null,
        CancellationToken ct = default)
    {
        var activities = await _activities.ListForProjectAsync(tenantId, projectId, ct);
        var items = await _items.ListByProjectAsync(tenantId, projectId, ct);

        var snapshots = new List<ItemVersionSnapshot>(items.Count);
        foreach (var item in items)
        {
            var revisions = await _revisions.ListByItemAsync(tenantId, item.Id, ct);
            var current = DetermineCurrent(revisions, asOf);
            snapshots.Add(new ItemVersionSnapshot(item.Id, current?.Id, current?.RevisionNumber ?? 0));
        }

        return new ProjectStateReconstruction(activities, snapshots);
    }

    private static KnowledgeRevision? DetermineCurrent(
        IReadOnlyList<KnowledgeRevision> revisions,
        DateTimeOffset? asOf)
    {
        if (revisions.Count == 0)
            return null;

        if (asOf is null)
            return revisions.OrderBy(r => r.RevisionNumber).Last();

        return revisions
            .Where(r => r.RecordedAt <= asOf.Value)
            .OrderBy(r => r.RevisionNumber)
            .LastOrDefault();
    }
}