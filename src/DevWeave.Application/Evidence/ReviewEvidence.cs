using DevWeave.Application.Abstractions;
using DevWeave.Domain.Evidence;
using DevWeave.Domain.Identity;
using EvidenceRecord = DevWeave.Domain.Evidence.Evidence;

namespace DevWeave.Application.Evidence;

/// <summary>
/// Records a governed review decision on a piece of evidence (SRS PRO-006). The reviewer must resolve
/// inside the tenant; illegal or no-op state transitions (enforced by the domain aggregate) surface as
/// <see cref="ConflictException"/>.
/// </summary>
public sealed class ReviewEvidence
{
    private readonly IEvidenceRepository _evidence;
    private readonly IPrincipalRepository _principals;
    private readonly IUnitOfWork _unitOfWork;

    public ReviewEvidence(
        IEvidenceRepository evidence,
        IPrincipalRepository principals,
        IUnitOfWork unitOfWork)
    {
        _evidence = evidence;
        _principals = principals;
        _unitOfWork = unitOfWork;
    }

    public async Task<EvidenceRecord> ExecuteAsync(
        TenantId tenantId,
        EvidenceId id,
        EvidenceReviewState state,
        PrincipalId reviewer,
        CancellationToken ct = default)
    {
        var record = await _evidence.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Evidence '{id}' was not found within tenant '{tenantId}'.");

        _ = await _principals.GetByIdAsync(tenantId, reviewer, ct)
            ?? throw new NotFoundException($"Principal '{reviewer}' was not found within tenant '{tenantId}'.");

        try
        {
            record.Review(state, reviewer);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return record;
    }
}