using DevWeave.Application.Abstractions;
using DevWeave.Domain.Activities;
using DevWeave.Domain.Artifacts;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using EvidenceRecord = DevWeave.Domain.Evidence.Evidence;

namespace DevWeave.Application.Evidence;

/// <summary>
/// Records a piece of evidence against a producing claim activity (SRS PRO-006). The origin activity
/// must exist inside the tenant and be a claim; a related artifact, when supplied, must also resolve
/// inside the tenant (deny-by-default). The criterion pin must resolve to a same-project revision that
/// belongs to the criterion item. The claim's project is adopted as the evidence's project.
/// </summary>
public sealed class RecordEvidence
{
    private readonly IEvidenceRepository _evidence;
    private readonly IActivityRepository _activities;
    private readonly IArtifactRepository _artifacts;
    private readonly IKnowledgeItemRepository _items;
    private readonly IKnowledgeRevisionRepository _revisions;
    private readonly IUnitOfWork _unitOfWork;

    public RecordEvidence(
        IEvidenceRepository evidence,
        IActivityRepository activities,
        IArtifactRepository artifacts,
        IKnowledgeItemRepository items,
        IKnowledgeRevisionRepository revisions,
        IUnitOfWork unitOfWork)
    {
        _evidence = evidence;
        _activities = activities;
        _artifacts = artifacts;
        _items = items;
        _revisions = revisions;
        _unitOfWork = unitOfWork;
    }

    public async Task<EvidenceRecord> ExecuteAsync(
        TenantId tenantId,
        ActivityId originActivityId,
        string type,
        string? integrityHash,
        ArtifactId? relatedArtifactId,
        KnowledgeItemId criterionItemId,
        KnowledgeRevisionId criterionRevisionId,
        string relatedCriterion,
        CancellationToken ct = default)
    {
        var claim = await _activities.GetByIdAsync(tenantId, originActivityId, ct)
            ?? throw new NotFoundException($"Claim activity '{originActivityId}' was not found within tenant '{tenantId}'.");

        if (claim.Type != ActivityType.Claim)
            throw new ConflictException(
                $"Activity '{originActivityId}' is not a claim and cannot receive evidence.");

        if (relatedArtifactId is { } artifactId)
        {
            _ = await _artifacts.GetByIdAsync(tenantId, artifactId, ct)
                ?? throw new NotFoundException($"Artifact '{artifactId}' was not found within tenant '{tenantId}'.");
        }

        var criterionItem = await _items.GetByIdAsync(tenantId, criterionItemId, ct)
            ?? throw new NotFoundException($"Criterion item '{criterionItemId}' was not found within tenant '{tenantId}'.");
        if (criterionItem.ProjectId != claim.ProjectId)
            throw new ConflictException("Evidence criterion must belong to the same project as the claim.");

        var criterionRevision = await _revisions.GetByIdAsync(tenantId, criterionRevisionId, ct)
            ?? throw new NotFoundException(
                $"Criterion revision '{criterionRevisionId}' was not found within tenant '{tenantId}'.");
        if (criterionRevision.KnowledgeItemId != criterionItemId)
            throw new ConflictException("Evidence criterion revision must belong to the criterion item.");

        var display = string.IsNullOrWhiteSpace(relatedCriterion)
            ? criterionItem.Title
            : relatedCriterion;

        var record = EvidenceRecord.Create(
            tenantId,
            claim.ProjectId,
            originActivityId,
            type,
            integrityHash,
            relatedArtifactId,
            criterionItemId,
            criterionRevisionId,
            display);

        await _evidence.AddAsync(record, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return record;
    }
}
