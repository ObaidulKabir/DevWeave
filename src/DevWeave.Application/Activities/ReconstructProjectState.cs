using DevWeave.Application.Abstractions;
using DevWeave.Domain.Activities;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Relations;
using EvidenceRecord = DevWeave.Domain.Evidence.Evidence;

namespace DevWeave.Application.Activities;

/// <summary>
/// Reconstructs project knowledge, activities, relationships, and evidence as-of recorded and
/// optional valid time (SRS REV-007, NFR-035). Recorded-time filters use Activity.StartedAt,
/// KnowledgeRevision.RecordedAt, Relationship.CreatedAt / RevokedAt, and Evidence.RecordedAt.
/// Valid-time filters use KnowledgeRevision.IsValidAt and Relationship ValidFrom/ValidTo when set.
/// </summary>
public sealed class ReconstructProjectState
{
    private readonly IActivityRepository _activities;
    private readonly IKnowledgeItemRepository _items;
    private readonly IKnowledgeRevisionRepository _revisions;
    private readonly IRelationshipRepository _relationships;
    private readonly IEvidenceRepository _evidence;

    public ReconstructProjectState(
        IActivityRepository activities,
        IKnowledgeItemRepository items,
        IKnowledgeRevisionRepository revisions,
        IRelationshipRepository relationships,
        IEvidenceRepository evidence)
    {
        _activities = activities;
        _items = items;
        _revisions = revisions;
        _relationships = relationships;
        _evidence = evidence;
    }

    public Task<ProjectStateReconstruction> ExecuteAsync(
        TenantId tenantId,
        ProjectId projectId,
        DateTimeOffset? asOf = null,
        CancellationToken ct = default) =>
        ExecuteAsync(tenantId, projectId, asOfRecorded: asOf, asOfValid: null, ct);

    public async Task<ProjectStateReconstruction> ExecuteAsync(
        TenantId tenantId,
        ProjectId projectId,
        DateTimeOffset? asOfRecorded,
        DateTimeOffset? asOfValid,
        CancellationToken ct = default)
    {
        var activities = await _activities.ListForProjectAsync(tenantId, projectId, ct);
        if (asOfRecorded is not null)
            activities = activities.Where(a => a.StartedAt <= asOfRecorded.Value).ToList();

        var items = await _items.ListByProjectAsync(tenantId, projectId, ct);
        var snapshots = new List<ItemVersionSnapshot>(items.Count);
        foreach (var item in items)
        {
            var revisions = await _revisions.ListByItemAsync(tenantId, item.Id, ct);
            var current = DetermineCurrent(revisions, asOfRecorded, asOfValid);
            snapshots.Add(new ItemVersionSnapshot(item.Id, current?.Id, current?.RevisionNumber ?? 0));
        }

        var relationships = await _relationships.ListByProjectAsync(tenantId, projectId, ct);
        relationships = FilterRelationships(relationships, asOfRecorded, asOfValid);

        var evidence = await _evidence.ListForProjectAsync(tenantId, projectId, ct);
        if (asOfRecorded is not null)
            evidence = evidence.Where(e => e.RecordedAt <= asOfRecorded.Value).ToList();

        return new ProjectStateReconstruction(activities, snapshots, relationships, evidence);
    }

    private static IReadOnlyList<Relationship> FilterRelationships(
        IReadOnlyList<Relationship> relationships,
        DateTimeOffset? asOfRecorded,
        DateTimeOffset? asOfValid)
    {
        IEnumerable<Relationship> candidates = relationships;

        if (asOfRecorded is { } recorded)
        {
            candidates = candidates.Where(r =>
                r.CreatedAt <= recorded &&
                (r.RevokedAt is null || r.RevokedAt > recorded));
        }
        else
        {
            candidates = candidates.Where(r => r.Status == RelationshipStatus.Active);
        }

        if (asOfValid is { } valid)
        {
            candidates = candidates.Where(r =>
                (r.ValidFrom is null || r.ValidFrom <= valid) &&
                (r.ValidTo is null || r.ValidTo >= valid));
        }

        return candidates.ToList();
    }

    private static KnowledgeRevision? DetermineCurrent(
        IReadOnlyList<KnowledgeRevision> revisions,
        DateTimeOffset? asOfRecorded,
        DateTimeOffset? asOfValid)
    {
        if (revisions.Count == 0)
            return null;

        IEnumerable<KnowledgeRevision> candidates = revisions;

        if (asOfRecorded is { } recorded)
            candidates = candidates.Where(r => r.RecordedAt <= recorded);

        if (asOfValid is { } valid)
            candidates = candidates.Where(r => r.IsValidAt(valid));

        return candidates
            .OrderBy(r => r.RevisionNumber)
            .LastOrDefault();
    }
}
