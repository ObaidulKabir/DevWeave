using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Relations;

namespace DevWeave.Application.Planning;

/// <summary>
/// Computes a governed impact closure for a <see cref="RequirementChangeSet"/> by walking active
/// semantic edges. Findings default to cautious dispositions; uncertainty never becomes Unaffected.
/// </summary>
public sealed class ImpactAnalysisService
{
    private readonly IKnowledgeRevisionRepository _revisions;
    private readonly IKnowledgeItemRepository _items;
    private readonly IRelationshipRepository _relationships;

    public ImpactAnalysisService(
        IKnowledgeRevisionRepository revisions,
        IKnowledgeItemRepository items,
        IRelationshipRepository relationships)
    {
        _revisions = revisions;
        _items = items;
        _relationships = relationships;
    }

    public async Task<ImpactAnalysisReport> AnalyzeAsync(
        RequirementChangeSet changeSet,
        PrincipalId analyzedBy,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(changeSet);
        ArgumentNullException.ThrowIfNull(analyzedBy);

        var seedItemIds = new HashSet<KnowledgeItemId>();
        var descriptors = changeSet.Changes.Select(entry => entry.ToDescriptor()).ToList();
        foreach (var change in descriptors)
        {
            foreach (var revisionId in change.PreviousRevisions.Concat(change.ResultRevisions))
            {
                var revision = await _revisions.GetByIdAsync(changeSet.TenantId, revisionId, ct)
                    ?? throw new NotFoundException(
                        $"Knowledge revision '{revisionId}' was not found within tenant '{changeSet.TenantId}'.");
                seedItemIds.Add(revision.KnowledgeItemId);
            }
        }

        var findings = new Dictionary<KnowledgeItemId, ImpactFinding>();
        var queue = new Queue<(KnowledgeItemId ItemId, string Path, RequirementChangeKind Kind)>();

        foreach (var itemId in seedItemIds)
        {
            foreach (var change in descriptors)
                queue.Enqueue((itemId, change.Kind.ToString(), change.Kind));
        }

        while (queue.Count > 0)
        {
            var (itemId, path, kind) = queue.Dequeue();
            var outgoing = await _relationships.ListOutgoingAsync(changeSet.TenantId, itemId, ct);
            var incoming = await _relationships.ListIncomingAsync(changeSet.TenantId, itemId, ct);

            foreach (var edge in outgoing.Concat(incoming)
                         .Where(edge =>
                             edge.Status == RelationshipStatus.Active &&
                             ImpactRelationCodes.Closure.Contains(edge.RelationTypeCode)))
            {
                var neighborId = edge.SourceItemId == itemId ? edge.TargetItemId : edge.SourceItemId;
                if (seedItemIds.Contains(neighborId) || findings.ContainsKey(neighborId))
                    continue;

                var neighbor = await _items.GetByIdAsync(changeSet.TenantId, neighborId, ct);
                if (neighbor is null || neighbor.ProjectId != changeSet.ProjectId)
                    continue;

                var disposition = DispositionFor(kind, edge.RelationTypeCode);
                var nextPath = $"{path}/{edge.RelationTypeCode}";
                findings[neighborId] = ImpactFinding.Create(
                    findings.Count,
                    neighborId,
                    neighbor.CurrentRevisionId,
                    neighbor.TypeId,
                    disposition,
                    nextPath,
                    $"impact-rules-1.0:{kind}:{edge.RelationTypeCode}");

                queue.Enqueue((neighborId, nextPath, kind));
            }
        }

        return ImpactAnalysisReport.Create(
            changeSet.TenantId,
            changeSet.ProjectId,
            changeSet.Id,
            analyzedBy,
            findings.Values.ToList());
    }

    private static ImpactDisposition DispositionFor(RequirementChangeKind kind, string relationTypeCode) =>
        (kind, relationTypeCode) switch
        {
            (RequirementChangeKind.Withdrawn, _) => ImpactDisposition.Orphaned,
            (RequirementChangeKind.Replaced, _) => ImpactDisposition.Superseded,
            (_, RelationTypeCodes.Validates) => ImpactDisposition.ReverificationRequired,
            (RequirementChangeKind.Clarified, _) => ImpactDisposition.ReviewRequired,
            (RequirementChangeKind.ThresholdTightened, _) => ImpactDisposition.ReverificationRequired,
            (RequirementChangeKind.ThresholdRelaxed, _) => ImpactDisposition.ReviewRequired,
            _ => ImpactDisposition.ReviewRequired,
        };
}
