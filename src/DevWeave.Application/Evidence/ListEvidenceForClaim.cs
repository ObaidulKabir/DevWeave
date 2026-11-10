using DevWeave.Application.Abstractions;
using DevWeave.Domain.Activities;
using DevWeave.Domain.Identity;
using EvidenceRecord = DevWeave.Domain.Evidence.Evidence;

namespace DevWeave.Application.Evidence;

/// <summary>Lists the evidence recorded for one claim activity, tenant-scoped (SRS PRO-006).</summary>
public sealed class ListEvidenceForClaim
{
    private readonly IEvidenceRepository _evidence;

    public ListEvidenceForClaim(IEvidenceRepository evidence)
    {
        _evidence = evidence;
    }

    public Task<IReadOnlyList<EvidenceRecord>> ExecuteAsync(
        TenantId tenantId,
        ActivityId originActivityId,
        CancellationToken ct = default) =>
        _evidence.ListForClaimAsync(tenantId, originActivityId, ct);
}