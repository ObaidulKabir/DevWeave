using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Knowledge;

/// <summary>
/// A tenant- and project-owning knowledge item (SRS KNW-001, KNW-002, KNW-003, KNW-006, KNW-011,
/// KNW-012, KNW-013, BR-004, BR-009). Identity is independent of any specific content representation:
/// the same item id is shared by every revision. The current-revision pointer is advanced atomically
/// on each new revision, and substantive changes always create a new revision (never mutate an old
/// one).
/// <para>
/// Four state dimensions are tracked independently (KNW-012): <see cref="State"/> (lifecycle),
/// <see cref="ReviewState"/> (human review), <see cref="VerificationState"/> and
/// <see cref="CurrencyState"/>. An item can therefore be active, approved, unverified and current at
/// the same time — approval never implies verification.
/// </para>
/// </summary>
public sealed class KnowledgeItem
{
    public KnowledgeItemId Id { get; }
    public TenantId TenantId { get; }
    public ProjectId ProjectId { get; }
    public string Key { get; }
    public string Title { get; }
    /// <summary>Optional human-readable details for the item identity (not revision content).</summary>
    public string Description { get; private set; }
    public Classification Classification { get; }
    public RepresentationKind RepresentationKind { get; }
    public InfoState State { get; private set; }
    public ReviewState ReviewState { get; private set; }
    public VerificationState VerificationState { get; private set; }
    public CurrencyState CurrencyState { get; private set; }
    public string TypeId { get; }
    public string SchemaVersion { get; }
    public KnowledgeRevisionId? CurrentRevisionId { get; private set; }
    public int CurrentRevisionNumber { get; private set; }
    public PrincipalId CreatedBy { get; }
    public DateTimeOffset CreatedAt { get; }

    private KnowledgeItem(
        KnowledgeItemId id,
        TenantId tenantId,
        ProjectId projectId,
        string key,
        string title,
        string description,
        Classification classification,
        RepresentationKind representationKind,
        InfoState state,
        ReviewState reviewState,
        VerificationState verificationState,
        CurrencyState currencyState,
        string typeId,
        string schemaVersion,
        KnowledgeRevisionId? currentRevisionId,
        int currentRevisionNumber,
        PrincipalId createdBy,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        Key = key;
        Title = title;
        Description = description;
        Classification = classification;
        RepresentationKind = representationKind;
        State = state;
        ReviewState = reviewState;
        VerificationState = verificationState;
        CurrencyState = currencyState;
        TypeId = typeId;
        SchemaVersion = schemaVersion;
        CurrentRevisionId = currentRevisionId;
        CurrentRevisionNumber = currentRevisionNumber;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public static KnowledgeItem Create(
        TenantId tenantId,
        ProjectId projectId,
        string key,
        string title,
        Classification classification,
        RepresentationKind representationKind,
        string typeId,
        string schemaVersion,
        PrincipalId createdBy,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Knowledge item key is required.", nameof(key));

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Knowledge item title is required.", nameof(title));

        if (string.IsNullOrWhiteSpace(typeId))
            throw new ArgumentException("Type identifier is required.", nameof(typeId));

        if (string.IsNullOrWhiteSpace(schemaVersion))
            throw new ArgumentException("Schema version is required.", nameof(schemaVersion));

        return new KnowledgeItem(
            KnowledgeItemId.New(),
            tenantId,
            projectId,
            key,
            title,
            description?.Trim() ?? string.Empty,
            classification,
            representationKind,
            InfoState.Proposed,
            ReviewState.Pending,
            VerificationState.Unverified,
            CurrencyState.Current,
            typeId,
            schemaVersion,
            currentRevisionId: null,
            currentRevisionNumber: 0,
            createdBy,
            DateTimeOffset.UtcNow);
    }

    /// <summary>Updates the item-level details text without creating a content revision.</summary>
    public void SetDescription(string? description)
    {
        Description = description?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Appends a new immutable revision and atomically advances the current-revision pointer
    /// (SRS REV-001, REV-004, BR-006). The caller must supply the item's current revision number as
    /// it last observed it; a mismatch raises <see cref="StaleRevisionException"/> so a stale write
    /// can never silently overwrite a committed revision.
    /// </summary>
    public KnowledgeRevision AddRevision(
        string schemaVersion,
        string content,
        PrincipalId actor,
        string reason,
        string? sourceReference,
        KnowledgeRevisionId? supersededRevisionId,
        int expectedCurrentRevisionNumber)
    {
        if (expectedCurrentRevisionNumber != CurrentRevisionNumber)
            throw new StaleRevisionException(expectedCurrentRevisionNumber, CurrentRevisionNumber);

        var revision = KnowledgeRevision.Create(
            TenantId,
            Id,
            CurrentRevisionNumber + 1,
            schemaVersion,
            content,
            actor,
            reason,
            sourceReference,
            supersededRevisionId);

        CurrentRevisionId = revision.Id;
        CurrentRevisionNumber = revision.RevisionNumber;

        // KNW-012 / PRO-005 / AC-08: approval and verification describe the content that was reviewed,
        // not the item's identity. A new revision is unreviewed, unverified content, so the review and
        // verification dimensions reset instead of letting new content inherit the prior revision's
        // approval — this is the revision-specific evidence invalidation that keeps stale acceptance
        // from applying to changed content.
        State = InfoState.Proposed;
        ReviewState = ReviewState.Pending;
        VerificationState = VerificationState.Unverified;

        return revision;
    }

    /// <summary>
    /// Approves the item and records that the human review dimension reached <c>Reviewed</c>
    /// (KNW-012, BR-009). Review, verification and currency stay independent, so the item remains
    /// unverified until qualifying evidence is accepted (PRO-005).
    /// </summary>
    public void Approve()
    {
        TransitionTo(InfoState.Approved);
        ReviewState = ReviewState.Reviewed;
    }

    public void Reject() => TransitionTo(InfoState.Rejected);

    /// <summary>
    /// Approves the item on the strength of a review of one specific revision. The decision is
    /// refused with <see cref="StaleReviewException"/> unless <paramref name="reviewedRevisionId"/> is
    /// still the current revision, so a reviewer can never approve content that changed after they
    /// read it (KNW-012, PRO-005).
    /// </summary>
    public void Approve(KnowledgeRevisionId reviewedRevisionId)
    {
        RequireReviewedRevisionIsCurrent(reviewedRevisionId);
        Approve();
    }

    /// <summary>Rejects the item on the strength of a review of one specific, current revision.</summary>
    public void Reject(KnowledgeRevisionId reviewedRevisionId)
    {
        RequireReviewedRevisionIsCurrent(reviewedRevisionId);
        Reject();
    }

    private void RequireReviewedRevisionIsCurrent(KnowledgeRevisionId reviewedRevisionId)
    {
        if (CurrentRevisionId is not { } current)
            throw new InvalidOperationException("Knowledge item has no revision to review.");

        if (current != reviewedRevisionId)
            throw new StaleReviewException(reviewedRevisionId, current);
    }

    /// <summary>
    /// Marks the item verified (BR-009). Verification is a claim about evidence, so it is refused
    /// unless the caller supplies that qualifying, policy-reviewed evidence exists — an
    /// agent-reported completion alone never counts as verified delivery (PRO-005, AC-08).
    /// </summary>
    public void Verify(bool hasQualifyingEvidence)
    {
        if (!hasQualifyingEvidence)
            throw new InvalidOperationException(
                "Knowledge item cannot be verified without qualifying evidence (PRO-005, AC-08).");

        TransitionTo(InfoState.Verified);
        VerificationState = VerificationState.Verified;
    }

    private void TransitionTo(InfoState target)
    {
        if (State == target)
            throw new InvalidOperationException($"Knowledge item already has state '{target}'.");

        if (!IsAllowedTransition(State, target))
            throw new InvalidOperationException($"Knowledge item cannot transition from '{State}' to '{target}'.");

        State = target;
    }

    private static bool IsAllowedTransition(InfoState from, InfoState to) =>
        (from, to) switch
        {
            (InfoState.Proposed, InfoState.Approved) => true,
            (InfoState.Proposed, InfoState.Rejected) => true,
            (InfoState.Approved, InfoState.Verified) => true,
            _ => false,
        };
}