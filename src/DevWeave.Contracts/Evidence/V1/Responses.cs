using DevWeave.Domain.Evidence;

namespace DevWeave.Contracts.Evidence.V1;

/// <summary>A piece of evidence with its origin, time, type, integrity and governed review state (SRS PRO-006).</summary>
public sealed record EvidenceResponse(
    Guid Id,
    Guid TenantId,
    Guid ProjectId,
    Guid OriginActivityId,
    DateTimeOffset RecordedAt,
    string Type,
    string? IntegrityHash,
    Guid? RelatedArtifactId,
    string RelatedCriterion,
    EvidenceReviewState ReviewState,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAt);

/// <summary>Whether a claim is verified, together with the evidence records that drove the verdict (SRS PRO-006).</summary>
public sealed record ClaimVerificationResponse(
    Guid ClaimId,
    Guid ProjectId,
    bool IsVerified,
    IReadOnlyList<EvidenceResponse> Evidence);

/// <summary>A structured evidence error body with a machine-readable code.</summary>
public sealed record EvidenceError(string Code, string Message);