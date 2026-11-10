using DevWeave.Application.Abstractions;
using DevWeave.Domain.Activities;
using DevWeave.Domain.Identity;
using EvidenceRecord = DevWeave.Domain.Evidence.Evidence;

namespace DevWeave.Application.Evidence;

/// <summary>
/// Evaluates whether a claim is verified by inspecting its evidence records (SRS PRO-006). The claim
/// must resolve inside the tenant; the returned verdict is true only when at least one piece of
/// evidence is accepted.
/// </summary>
public sealed class GetClaimVerification
{
    private readonly IActivityRepository _activities;
    private readonly IEvidenceRepository _evidence;

    public GetClaimVerification(IActivityRepository activities, IEvidenceRepository evidence)
    {
        _activities = activities;
        _evidence = evidence;
    }

    public async Task<ClaimVerificationResult> ExecuteAsync(
        TenantId tenantId,
        ActivityId claimId,
        CancellationToken ct = default)
    {
        var claim = await _activities.GetByIdAsync(tenantId, claimId, ct)
            ?? throw new NotFoundException($"Claim activity '{claimId}' was not found within tenant '{tenantId}'.");

        var records = await _evidence.ListForClaimAsync(tenantId, claimId, ct);

        return new ClaimVerificationResult(
            claim.Id.Value,
            claim.ProjectId,
            EvidenceRecord.ClaimIsVerified(records),
            records);
    }
}