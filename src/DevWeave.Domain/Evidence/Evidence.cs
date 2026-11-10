using DevWeave.Domain.Activities;
using DevWeave.Domain.Artifacts;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Evidence;

/// <summary>
/// A piece of evidence attached to a producing origin activity (a claim), kept distinct from the claim
/// itself (SRS PRO-006). Evidence carries origin, time, type, integrity metadata, the related criterion,
/// and a governed review state. A claim remains unverified until it has accepted evidence.
/// </summary>
public sealed class Evidence
{
    public EvidenceId Id { get; }
    public TenantId TenantId { get; }
    public ProjectId ProjectId { get; }
    public ActivityId OriginActivityId { get; }
    public DateTimeOffset RecordedAt { get; }
    public string Type { get; }
    public string? IntegrityHash { get; }
    public ArtifactId? RelatedArtifactId { get; }
    public string RelatedCriterion { get; }
    public EvidenceReviewState ReviewState { get; private set; }
    public PrincipalId? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }

    private Evidence(
        EvidenceId id,
        TenantId tenantId,
        ProjectId projectId,
        ActivityId originActivityId,
        DateTimeOffset recordedAt,
        string type,
        string? integrityHash,
        ArtifactId? relatedArtifactId,
        string relatedCriterion,
        EvidenceReviewState reviewState,
        PrincipalId? reviewedBy,
        DateTimeOffset? reviewedAt)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        OriginActivityId = originActivityId;
        RecordedAt = recordedAt;
        Type = type;
        IntegrityHash = integrityHash;
        RelatedArtifactId = relatedArtifactId;
        RelatedCriterion = relatedCriterion;
        ReviewState = reviewState;
        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAt;
    }

    public static Evidence Create(
        TenantId tenantId,
        ProjectId projectId,
        ActivityId originActivityId,
        string type,
        string? integrityHash,
        ArtifactId? relatedArtifactId,
        string relatedCriterion)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Evidence type is required.", nameof(type));

        if (string.IsNullOrWhiteSpace(relatedCriterion))
            throw new ArgumentException("Evidence related criterion is required.", nameof(relatedCriterion));

        return new Evidence(
            EvidenceId.New(),
            tenantId,
            projectId,
            originActivityId,
            DateTimeOffset.UtcNow,
            type,
            integrityHash,
            relatedArtifactId,
            relatedCriterion,
            EvidenceReviewState.Unreviewed,
            reviewedBy: null,
            reviewedAt: null);
    }

    /// <summary>
    /// Records a governed review decision (PRO-006). Illegal and no-op transitions are rejected so a
    /// review can never silently regress or be replayed.
    /// </summary>
    public void Review(EvidenceReviewState state, PrincipalId reviewer)
    {
        if (state == ReviewState)
            throw new InvalidOperationException($"Evidence already has review state '{state}'.");

        if (!IsAllowedTransition(ReviewState, state))
            throw new InvalidOperationException($"Evidence cannot transition from '{ReviewState}' to '{state}'.");

        ReviewState = state;
        ReviewedBy = reviewer;
        ReviewedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>A claim is verified only when at least one of its evidence records is accepted (PRO-006).</summary>
    public static bool ClaimIsVerified(IReadOnlyList<Evidence> evidence) =>
        evidence.Any(e => e.ReviewState == EvidenceReviewState.Accepted);

    private static bool IsAllowedTransition(EvidenceReviewState from, EvidenceReviewState to) =>
        (from, to) switch
        {
            (EvidenceReviewState.Unreviewed, EvidenceReviewState.Accepted) => true,
            (EvidenceReviewState.Unreviewed, EvidenceReviewState.Rejected) => true,
            (EvidenceReviewState.Unreviewed, EvidenceReviewState.Contested) => true,
            (EvidenceReviewState.Accepted, EvidenceReviewState.Contested) => true,
            (EvidenceReviewState.Rejected, EvidenceReviewState.Contested) => true,
            (EvidenceReviewState.Contested, EvidenceReviewState.Accepted) => true,
            (EvidenceReviewState.Contested, EvidenceReviewState.Rejected) => true,
            _ => false,
        };
}