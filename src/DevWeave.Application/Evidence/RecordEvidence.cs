using DevWeave.Application.Abstractions;
using DevWeave.Domain.Activities;
using DevWeave.Domain.Artifacts;
using DevWeave.Domain.Identity;
using EvidenceRecord = DevWeave.Domain.Evidence.Evidence;

namespace DevWeave.Application.Evidence;

/// <summary>
/// Records a piece of evidence against a producing claim activity (SRS PRO-006). The origin activity
/// must exist inside the tenant and be a claim; a related artifact, when supplied, must also resolve
/// inside the tenant (deny-by-default). The claim's project is adopted as the evidence's project.
/// </summary>
public sealed class RecordEvidence
{
    private readonly IEvidenceRepository _evidence;
    private readonly IActivityRepository _activities;
    private readonly IArtifactRepository _artifacts;
    private readonly IUnitOfWork _unitOfWork;

    public RecordEvidence(
        IEvidenceRepository evidence,
        IActivityRepository activities,
        IArtifactRepository artifacts,
        IUnitOfWork unitOfWork)
    {
        _evidence = evidence;
        _activities = activities;
        _artifacts = artifacts;
        _unitOfWork = unitOfWork;
    }

    public async Task<EvidenceRecord> ExecuteAsync(
        TenantId tenantId,
        ActivityId originActivityId,
        string type,
        string? integrityHash,
        ArtifactId? relatedArtifactId,
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

        var record = EvidenceRecord.Create(
            tenantId,
            claim.ProjectId,
            originActivityId,
            type,
            integrityHash,
            relatedArtifactId,
            relatedCriterion);

        await _evidence.AddAsync(record, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return record;
    }
}