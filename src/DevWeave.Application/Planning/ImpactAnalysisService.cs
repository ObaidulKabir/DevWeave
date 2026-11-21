using DevWeave.Application.Abstractions;
using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Relations;

namespace DevWeave.Application.Planning;

/// <summary>
/// Computes a governed impact closure for a <see cref="RequirementChangeSet"/> by walking active
/// semantic edges and open work-package scope. Findings default to cautious dispositions; uncertainty
/// never becomes Unaffected. Split/merge never copies historical links onto new revisions.
/// </summary>
public sealed class ImpactAnalysisService
{
    private readonly IKnowledgeRevisionRepository _revisions;
    private readonly IKnowledgeItemRepository _items;
    private readonly IRelationshipRepository _relationships;
    private readonly IWorkPackageImpactSource? _workPackages;

    public ImpactAnalysisService(
        IKnowledgeRevisionRepository revisions,
        IKnowledgeItemRepository items,
        IRelationshipRepository relationships,
        IWorkPackageImpactSource? workPackages = null)
    {
        _revisions = revisions;
        _items = items;
        _relationships = relationships;
        _workPackages = workPackages;
    }

    public async Task<ImpactAnalysisReport> AnalyzeAsync(
        RequirementChangeSet changeSet,
        PrincipalId analyzedBy,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(changeSet);
        ArgumentNullException.ThrowIfNull(analyzedBy);

        var seedItemIds = new HashSet<KnowledgeItemId>();
        var changedRevisionIds = new HashSet<Guid>();
        var descriptors = changeSet.Changes.Select(entry => entry.ToDescriptor()).ToList();
        foreach (var change in descriptors)
        {
            foreach (var revisionId in change.PreviousRevisions.Concat(change.ResultRevisions))
            {
                var revision = await _revisions.GetByIdAsync(changeSet.TenantId, revisionId, ct)
                    ?? throw new NotFoundException(
                        $"Knowledge revision '{revisionId}' was not found within tenant '{changeSet.TenantId}'.");
                seedItemIds.Add(revision.KnowledgeItemId);
                changedRevisionIds.Add(revisionId.Value);
            }
        }

        var findings = new Dictionary<string, ImpactFinding>();
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
                var findingKey = $"knowledge:{neighborId.Value:N}";
                if (seedItemIds.Contains(neighborId) || findings.ContainsKey(findingKey))
                    continue;

                var neighbor = await _items.GetByIdAsync(changeSet.TenantId, neighborId, ct);
                if (neighbor is null || neighbor.ProjectId != changeSet.ProjectId)
                    continue;

                var disposition = DispositionFor(kind, edge.RelationTypeCode);
                var nextPath = $"{path}/{edge.RelationTypeCode}";
                findings[findingKey] = ImpactFinding.Create(
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

        if (_workPackages is not null)
        {
            var packages = await _workPackages.ListWorkPackagesByProjectAsync(
                changeSet.TenantId, changeSet.ProjectId, ct);
            foreach (var package in packages.Where(package => package.IsOpen))
            {
                var kind = PrimaryKind(descriptors);
                var findingKey = $"work.package:{package.Id.Value:N}";
                if (findings.ContainsKey(findingKey))
                    continue;

                if (package.IsUnscoped)
                {
                    findings[findingKey] = ImpactFinding.ForResource(
                        findings.Count,
                        package.Id.Value,
                        null,
                        "work.package",
                        ImpactDisposition.Blocked,
                        $"{kind}/unscoped",
                        $"impact-rules-1.0:{kind}:unscoped-package");
                    continue;
                }

                if (!package.ScopeRevisionIds.Any(changedRevisionIds.Contains))
                    continue;

                findings[findingKey] = ImpactFinding.ForResource(
                    findings.Count,
                    package.Id.Value,
                    null,
                    "work.package",
                    DispositionForPackage(kind),
                    $"{kind}/work-package-scope",
                    $"impact-rules-1.0:{kind}:work-package-scope");
            }
        }

        return ImpactAnalysisReport.Create(
            changeSet.TenantId,
            changeSet.ProjectId,
            changeSet.Id,
            analyzedBy,
            findings.Values.ToList());
    }

    private static RequirementChangeKind PrimaryKind(IReadOnlyList<RequirementChangeDescriptor> changes) =>
        changes[0].Kind;

    private static ImpactDisposition DispositionFor(RequirementChangeKind kind, string relationTypeCode) =>
        (kind, relationTypeCode) switch
        {
            (RequirementChangeKind.Withdrawn, _) => ImpactDisposition.Orphaned,
            (RequirementChangeKind.Replaced, _) => ImpactDisposition.Superseded,
            (RequirementChangeKind.Split, _) => ImpactDisposition.ReviewRequired,
            (RequirementChangeKind.Merged, _) => ImpactDisposition.ReviewRequired,
            (_, RelationTypeCodes.Validates) => ImpactDisposition.ReverificationRequired,
            (RequirementChangeKind.Clarified, _) => ImpactDisposition.ReviewRequired,
            (RequirementChangeKind.ThresholdTightened, _) => ImpactDisposition.ReverificationRequired,
            (RequirementChangeKind.ThresholdRelaxed, _) => ImpactDisposition.ReviewRequired,
            _ => ImpactDisposition.ReviewRequired,
        };

    private static ImpactDisposition DispositionForPackage(RequirementChangeKind kind) =>
        kind switch
        {
            RequirementChangeKind.Withdrawn => ImpactDisposition.Orphaned,
            RequirementChangeKind.Replaced => ImpactDisposition.Superseded,
            RequirementChangeKind.ThresholdTightened => ImpactDisposition.ReverificationRequired,
            _ => ImpactDisposition.ReviewRequired,
        };
}
