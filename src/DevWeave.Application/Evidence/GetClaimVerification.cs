using DevWeave.Application.Abstractions;
using DevWeave.Domain.Activities;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using EvidenceRecord = DevWeave.Domain.Evidence.Evidence;

namespace DevWeave.Application.Evidence;

/// <summary>
/// Evaluates whether a claim is verified by inspecting its evidence records (SRS PRO-006). The claim
/// must resolve inside the tenant; the returned verdict is true only when at least one piece of
/// accepted evidence still pins the criterion item's current revision.
/// </summary>
public sealed class GetClaimVerification
{
    private readonly IActivityRepository _activities;
    private readonly IEvidenceRepository _evidence;
    private readonly IKnowledgeItemRepository _items;

    public GetClaimVerification(
        IActivityRepository activities,
        IEvidenceRepository evidence,
        IKnowledgeItemRepository items)
    {
        _activities = activities;
        _evidence = evidence;
        _items = items;
    }

    public async Task<ClaimVerificationResult> ExecuteAsync(
        TenantId tenantId,
        ActivityId claimId,
        CancellationToken ct = default)
    {
        var claim = await _activities.GetByIdAsync(tenantId, claimId, ct)
            ?? throw new NotFoundException($"Claim activity '{claimId}' was not found within tenant '{tenantId}'.");

        var records = await _evidence.ListForClaimAsync(tenantId, claimId, ct);
        var criterionIds = records
            .Where(record => record.CriterionItemId is not null)
            .Select(record => record.CriterionItemId!)
            .Distinct()
            .ToArray();
        var currentByItem = new Dictionary<KnowledgeItemId, KnowledgeRevisionId?>();
        foreach (var criterionId in criterionIds)
        {
            var item = await _items.GetByIdAsync(tenantId, criterionId, ct);
            currentByItem[criterionId] = item?.CurrentRevisionId;
        }

        var isVerified = EvidenceRecord.ClaimIsVerified(
            records,
            itemId => currentByItem.GetValueOrDefault(itemId));

        return new ClaimVerificationResult(
            claim.Id.Value,
            claim.ProjectId,
            isVerified,
            records);
    }
}
