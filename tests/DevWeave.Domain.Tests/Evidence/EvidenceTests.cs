using DevWeave.Domain.Activities;
using DevWeave.Domain.Artifacts;
using DevWeave.Domain.Evidence;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;
using EvidenceRecord = DevWeave.Domain.Evidence.Evidence;

namespace DevWeave.Domain.Tests.Evidence;

/// <summary>
/// Proves the Evidence domain invariants for Slice 7 (PRO-006): origin/time/type/integrity metadata,
/// revision-pinned criteria, the governed review-state transitions, and the claim-verification rule
/// that a claim is verified only when accepted evidence still pins the current criterion revision.
/// </summary>
public sealed class EvidenceTests
{
    private static readonly string[] ExpectedStates = ["Unreviewed", "Accepted", "Rejected", "Contested"];

    [Fact]
    public void PRO_006_creation_populates_origin_time_type_integrity_and_criterion_pin()
    {
        var (tenant, project) = Seed();
        var activityId = ActivityId.New();
        var artifactId = ArtifactId.New();
        var criterionItemId = KnowledgeItemId.New();
        var criterionRevisionId = KnowledgeRevisionId.New();

        var evidence = EvidenceRecord.Create(
            tenant,
            project,
            activityId,
            "screenshot",
            "sha256:aa",
            artifactId,
            criterionItemId,
            criterionRevisionId,
            "AC-1");

        Assert.Equal(tenant, evidence.TenantId);
        Assert.Equal(project, evidence.ProjectId);
        Assert.Equal(activityId, evidence.OriginActivityId);
        Assert.NotEqual(default, evidence.RecordedAt);
        Assert.Equal("screenshot", evidence.Type);
        Assert.Equal("sha256:aa", evidence.IntegrityHash);
        Assert.Equal(artifactId, evidence.RelatedArtifactId);
        Assert.Equal(criterionItemId, evidence.CriterionItemId);
        Assert.Equal(criterionRevisionId, evidence.CriterionRevisionId);
        Assert.Equal("AC-1", evidence.RelatedCriterion);
        Assert.Equal(EvidenceReviewState.Unreviewed, evidence.ReviewState);
        Assert.Null(evidence.ReviewedBy);
        Assert.Null(evidence.ReviewedAt);
    }

    [Fact]
    public void PRO_006_new_evidence_starts_unreviewed_and_unvalidated()
    {
        var evidence = CreateEvidence();

        Assert.Equal(EvidenceReviewState.Unreviewed, evidence.ReviewState);
        Assert.Null(evidence.ReviewedBy);
        Assert.Null(evidence.ReviewedAt);
    }

    [Fact]
    public void PRO_006_review_records_reviewer_and_time_and_transitions_state()
    {
        var evidence = CreateEvidence();
        var reviewer = PrincipalId.New();

        evidence.Review(EvidenceReviewState.Accepted, reviewer);

        Assert.Equal(EvidenceReviewState.Accepted, evidence.ReviewState);
        Assert.Equal(reviewer, evidence.ReviewedBy);
        Assert.NotNull(evidence.ReviewedAt);
    }

    [Fact]
    public void PRO_006_claim_is_verified_only_with_accepted_current_criterion_pin()
    {
        var criterionItemId = KnowledgeItemId.New();
        var currentRevisionId = KnowledgeRevisionId.New();
        var staleRevisionId = KnowledgeRevisionId.New();

        Assert.False(EvidenceRecord.ClaimIsVerified(Array.Empty<EvidenceRecord>(), _ => currentRevisionId));
        Assert.False(EvidenceRecord.ClaimIsVerified(
            [CreateEvidence(criterionItemId, currentRevisionId)],
            _ => currentRevisionId));

        var acceptedCurrent = CreateEvidence(criterionItemId, currentRevisionId, EvidenceReviewState.Accepted);
        Assert.True(EvidenceRecord.ClaimIsVerified([acceptedCurrent], _ => currentRevisionId));

        var acceptedStale = CreateEvidence(criterionItemId, staleRevisionId, EvidenceReviewState.Accepted);
        Assert.False(EvidenceRecord.ClaimIsVerified([acceptedStale], _ => currentRevisionId));
    }

    [Fact]
    public void PRO_006_contested_evidence_does_not_verify_a_claim()
    {
        var criterionItemId = KnowledgeItemId.New();
        var revisionId = KnowledgeRevisionId.New();
        var contested = CreateEvidence(criterionItemId, revisionId);
        contested.Review(EvidenceReviewState.Contested, PrincipalId.New());

        Assert.False(EvidenceRecord.ClaimIsVerified([contested], _ => revisionId));
    }

    [Fact]
    public void review_rejects_illegal_and_noop_transitions()
    {
        var evidence = CreateEvidence();
        var reviewer = PrincipalId.New();

        evidence.Review(EvidenceReviewState.Accepted, reviewer);

        Assert.Throws<InvalidOperationException>(() => evidence.Review(EvidenceReviewState.Accepted, reviewer));
        Assert.Throws<InvalidOperationException>(() => evidence.Review(EvidenceReviewState.Rejected, reviewer));

        evidence.Review(EvidenceReviewState.Contested, reviewer);
        evidence.Review(EvidenceReviewState.Rejected, reviewer);
        Assert.Equal(EvidenceReviewState.Rejected, evidence.ReviewState);
    }

    [Fact]
    public void PRO_006_review_state_domain_is_exactly_the_four_states()
    {
        Assert.Equal(ExpectedStates, Enum.GetNames<EvidenceReviewState>());
    }

    [Fact]
    public void creation_guards_required_type_and_criterion()
    {
        var (tenant, project) = Seed();

        Assert.Throws<ArgumentException>(() =>
            EvidenceRecord.Create(
                tenant,
                project,
                ActivityId.New(),
                " ",
                null,
                null,
                KnowledgeItemId.New(),
                KnowledgeRevisionId.New(),
                "AC-1"));
        Assert.Throws<ArgumentException>(() =>
            EvidenceRecord.Create(
                tenant,
                project,
                ActivityId.New(),
                "screenshot",
                null,
                null,
                KnowledgeItemId.New(),
                KnowledgeRevisionId.New(),
                " "));
    }

    [Fact]
    public void review_state_is_the_only_mutable_surface()
    {
        var writable = typeof(EvidenceRecord)
            .GetProperties()
            .Where(p => p.CanWrite)
            .Select(p => p.Name)
            .ToArray();

        Assert.Equal(
            new[] { nameof(EvidenceRecord.ReviewState), nameof(EvidenceRecord.ReviewedBy), nameof(EvidenceRecord.ReviewedAt) },
            writable);
    }

    private static (TenantId Tenant, ProjectId Project) Seed() =>
        (TenantId.New(), ProjectId.New());

    private static EvidenceRecord CreateEvidence(
        KnowledgeItemId? criterionItemId = null,
        KnowledgeRevisionId? criterionRevisionId = null,
        EvidenceReviewState? state = null)
    {
        var evidence = EvidenceRecord.Create(
            TenantId.New(),
            ProjectId.New(),
            ActivityId.New(),
            "screenshot",
            "sha256:aa",
            ArtifactId.New(),
            criterionItemId ?? KnowledgeItemId.New(),
            criterionRevisionId ?? KnowledgeRevisionId.New(),
            "AC-1");

        if (state is { } target)
            evidence.Review(target, PrincipalId.New());

        return evidence;
    }
}
