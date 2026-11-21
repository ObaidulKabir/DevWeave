using DevWeave.Domain.Activities;
using DevWeave.Domain.Artifacts;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Evidence;

/// <summary>
/// A piece of evidence attached to a producing origin activity (a claim), kept distinct from the claim
/// itself (SRS PRO-006). Evidence carries origin, time, type, integrity metadata, a revision-pinned
/// criterion, optional display text, and a governed review state. A claim remains unverified until it
/// has accepted evidence whose criterion pin still matches the criterion item's current revision.
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
    public KnowledgeItemId? CriterionItemId { get; }
    public KnowledgeRevisionId? CriterionRevisionId { get; }
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
        KnowledgeItemId? criterionItemId,
        KnowledgeRevisionId? criterionRevisionId,
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
        CriterionItemId = criterionItemId;
        CriterionRevisionId = criterionRevisionId;
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
        KnowledgeItemId criterionItemId,
        KnowledgeRevisionId criterionRevisionId,
        string relatedCriterion)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Evidence type is required.", nameof(type));
        ArgumentNullException.ThrowIfNull(criterionItemId);
        ArgumentNullException.ThrowIfNull(criterionRevisionId);
        if (string.IsNullOrWhiteSpace(relatedCriterion))
            throw new ArgumentException("Evidence related criterion display text is required.", nameof(relatedCriterion));

        return new Evidence(
            EvidenceId.New(),
            tenantId,
            projectId,
            originActivityId,
            DateTimeOffset.UtcNow,
            type.Trim(),
            integrityHash,
            relatedArtifactId,
            criterionItemId,
            criterionRevisionId,
            relatedCriterion.Trim(),
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

    /// <summary>
    /// Accepted evidence verifies a claim only when its criterion pin is present and still equals the
    /// criterion item's current revision.
    /// </summary>
    public bool IsCurrentAcceptedVerification(KnowledgeRevisionId? currentCriterionRevisionId) =>
        ReviewState == EvidenceReviewState.Accepted &&
        CriterionItemId is not null &&
        CriterionRevisionId is not null &&
        currentCriterionRevisionId is not null &&
        CriterionRevisionId == currentCriterionRevisionId;

    /// <summary>A claim is verified only when at least one evidence record is currently accepted.</summary>
    public static bool ClaimIsVerified(
        IReadOnlyList<Evidence> evidence,
        Func<KnowledgeItemId, KnowledgeRevisionId?> currentRevisionOf)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(currentRevisionOf);

        return evidence.Any(record =>
            record.CriterionItemId is { } itemId &&
            record.IsCurrentAcceptedVerification(currentRevisionOf(itemId)));
    }

    /// <summary>Legacy helper: acceptance alone is insufficient once criterion pins exist.</summary>
    public static bool ClaimIsVerified(IReadOnlyList<Evidence> evidence) =>
        ClaimIsVerified(evidence, _ => null);

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
