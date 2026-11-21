using DevWeave.Domain.Evidence;

namespace DevWeave.Contracts.Evidence.V1;

/// <summary>
/// Records evidence against a claim activity (SRS PRO-006). The related artifact is optional; when
/// supplied it must resolve inside the tenant. Criterion item and revision pins are required; display
/// text defaults to the criterion item title when omitted. The claim's project is adopted from the
/// claim itself.
/// </summary>
public sealed record RecordEvidenceRequest(
    string Type,
    string? IntegrityHash,
    Guid? RelatedArtifactId,
    Guid CriterionItemId,
    Guid CriterionRevisionId,
    string? RelatedCriterion = null);

/// <summary>Records a governed review decision on a piece of evidence (SRS PRO-006).</summary>
public sealed record ReviewEvidenceRequest(
    EvidenceReviewState State,
    Guid Reviewer);
