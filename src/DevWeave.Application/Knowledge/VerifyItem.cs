using DevWeave.Application.Abstractions;
using DevWeave.Domain.Activities;
using DevWeave.Domain.Evidence;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using EvidenceRecord = DevWeave.Domain.Evidence.Evidence;

namespace DevWeave.Application.Knowledge;

/// <summary>
/// Marks a knowledge item verified (SRS BR-009). Verification is gated on qualifying evidence:
/// agent-reported completion never counts as verified delivery, so the item is only advanced once a
/// claim pinned to the item's current revision has at least one accepted evidence record
/// (KNW-012, PRO-005, AC-08).
/// </summary>
public sealed class VerifyItem
{
    private readonly IKnowledgeItemRepository _items;
    private readonly IActivityRepository _activities;
    private readonly IEvidenceRepository _evidence;
    private readonly IUnitOfWork _unitOfWork;

    public VerifyItem(
        IKnowledgeItemRepository items,
        IActivityRepository activities,
        IEvidenceRepository evidence,
        IUnitOfWork unitOfWork)
    {
        _items = items;
        _activities = activities;
        _evidence = evidence;
        _unitOfWork = unitOfWork;
    }

    public async Task<KnowledgeItem> ExecuteAsync(TenantId tenantId, KnowledgeItemId id, CancellationToken ct = default)
    {
        var item = await _items.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Knowledge item '{id}' was not found within tenant '{tenantId}'.");

        // The domain owns the rule (PRO-005/AC-08); this use case supplies the evidence fact.
        var hasQualifyingEvidence = await HasQualifyingEvidenceAsync(tenantId, item, ct);

        item.Verify(hasQualifyingEvidence);

        await _unitOfWork.SaveChangesAsync(ct);

        return item;
    }

    private async Task<bool> HasQualifyingEvidenceAsync(TenantId tenantId, KnowledgeItem item, CancellationToken ct)
    {
        // Revision-specific: only evidence for a claim that pins the item's *current* revision can
        // qualify it, so a stale claim from a superseded revision can never re-verify changed content.
        var currentRevisionId = item.CurrentRevisionId;
        if (currentRevisionId is null)
            return false;

        var activities = await _activities.ListForItemAsync(tenantId, item.Id, ct);

        foreach (var claim in activities.Where(a =>
                     a.Type == ActivityType.Claim
                     && a.Outputs.Any(o => o.ItemId == item.Id && o.RevisionId == currentRevisionId)))
        {
            var evidence = await _evidence.ListForClaimAsync(tenantId, claim.Id, ct);
            if (EvidenceRecord.ClaimIsVerified(evidence))
                return true;
        }

        return false;
    }
}
