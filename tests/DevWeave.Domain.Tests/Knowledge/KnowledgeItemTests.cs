using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Tests.Knowledge;

/// <summary>
/// Proves the Knowledge domain invariants for Slice 3: stable identity across revisions (KNOW-001),
/// pinned type/schema (KNW-002), typed envelope metadata (KNW-003), representation kinds
/// (KNW-005), separation of identity/revision (KNW-006), information-state transitions (KNW-011,
/// BR-009), independent lifecycle/review/verification/currency dimensions (KNW-012), provenance
/// (KNW-013), immutable revisions (REV-001, BR-006), optimistic concurrency
/// (REV-004), correction/supersession (REV-008), retained schema version (BR-005) and stable
/// identity (BR-004).
/// </summary>
public sealed class KnowledgeItemTests
{
    private static readonly string[] ExpectedClassificationTiers = ["Public", "Internal", "Confidential", "Restricted"];
    private static readonly string[] ExpectedRepresentationKinds = ["Text", "StructuredData", "Diagram", "Chart", "Document", "Media", "ExternalReference", "SourceCode"];

    [Fact]
    public void BR_004_creation_establishes_stable_identity_and_scope()
    {
        var tenant = Tenant.Create("Alpha Corp");
        var project = ProjectId.New();
        var actor = PrincipalId.New();

        var item = KnowledgeItem.Create(
            tenant.Id, project, "api-design", "API Design Notes", Classification.Internal,
            RepresentationKind.Text, "doc.note", "1.0", actor);

        Assert.NotEqual(Guid.Empty, item.Id.Value);
        Assert.Equal(tenant.Id, item.TenantId);
        Assert.Equal(project, item.ProjectId);
        Assert.Equal("api-design", item.Key);
        Assert.Equal(actor, item.CreatedBy);
        Assert.NotEqual(default, item.CreatedAt);
    }

    [Fact]
    public void BR_004_key_is_immutable_after_creation()
    {
        var key = typeof(KnowledgeItem).GetProperty(nameof(KnowledgeItem.Key))!;

        Assert.False(key.CanWrite);
    }

    [Fact]
    public void KNW_002_creation_pins_type_id_repr_and_schema_version()
    {
        var item = CreateItem(typeId: "spec.decision-record", schemaVersion: "2.1", representation: RepresentationKind.Document);

        Assert.Equal("spec.decision-record", item.TypeId);
        Assert.Equal("2.1", item.SchemaVersion);
        Assert.Equal(RepresentationKind.Document, item.RepresentationKind);

        Assert.False(typeof(KnowledgeItem).GetProperty(nameof(KnowledgeItem.TypeId))!.CanWrite);
        Assert.False(typeof(KnowledgeItem).GetProperty(nameof(KnowledgeItem.SchemaVersion))!.CanWrite);
    }

    [Fact]
    public void KNW_003_envelope_metadata_is_populated_and_typed()
    {
        var item = CreateItem(classification: Classification.Confidential);

        Assert.Equal(Classification.Confidential, item.Classification);
        Assert.Equal("API Design Notes", item.Title);
        Assert.Equal(InfoState.Proposed, item.State);
        Assert.Equal(ReviewState.Pending, item.ReviewState);
        Assert.Equal(VerificationState.Unverified, item.VerificationState);
        Assert.Equal(CurrencyState.Current, item.CurrencyState);
        Assert.Null(item.CurrentRevisionId);
        Assert.Equal(0, item.CurrentRevisionNumber);
    }

    [Fact]
    public void KNW_003_classification_tiers_are_exactly_the_four_ordered_tiers()
    {
        Assert.Equal(ExpectedClassificationTiers, Enum.GetNames<Classification>());
    }

    [Fact]
    public void KNW_005_representation_kinds_cover_all_seven_kinds()
    {
        Assert.Equal(ExpectedRepresentationKinds, Enum.GetNames<RepresentationKind>());
    }

    [Fact]
    public void KNW_011_creation_defaults_to_proposed_even_for_agent_created_content()
    {
        var agent = PrincipalId.New();

        var item = KnowledgeItem.Create(
            TenantId.New(), ProjectId.New(), "gen-summary", "Generated summary",
            Classification.Internal, RepresentationKind.Text, "agent.summary", "1.0", agent);

        // Generated/agent-created content can never self-approve merely by being stored.
        Assert.Equal(InfoState.Proposed, item.State);
        Assert.Equal(VerificationState.Unverified, item.VerificationState);
    }

    [Fact]
    public void KNW_001_identity_is_stable_across_revisions()
    {
        var (tenant, project, actor) = Seed();
        var item = CreateItem(tenant, project, actor);
        var first = item.AddRevision("1.0", "{\"v\":1}", actor, "seed", null, null, 0);
        var second = item.AddRevision("1.0", "{\"v\":2}", actor, "update", null, null, 1);

        Assert.Equal(item.Id, first.KnowledgeItemId);
        Assert.Equal(item.Id, second.KnowledgeItemId);
        Assert.Equal(first.KnowledgeItemId, second.KnowledgeItemId);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void REV_002_revision_records_number_schema_actor_time_reason_and_source()
    {
        var (tenant, project, actor) = Seed();
        var item = CreateItem(tenant, project, actor);

        var revision = item.AddRevision("3.4", "{\"body\":\"hello\"}", actor, "initial capture", "doc-source-1", null, 0);

        Assert.Equal(1, revision.RevisionNumber);
        Assert.Equal("3.4", revision.SchemaVersion);
        Assert.Equal("{\"body\":\"hello\"}", revision.Content);
        Assert.Equal(actor, revision.Actor);
        Assert.NotEqual(default, revision.RecordedAt);
        Assert.Equal("initial capture", revision.Reason);
        Assert.Equal("doc-source-1", revision.SourceReference);
    }

    [Fact]
    public void KNW_013_source_attribution_is_retained_on_item_and_revision()
    {
        var (tenant, project, actor) = Seed();
        var item = CreateItem(tenant, project, actor);
        var revision = item.AddRevision("1.0", "{}", actor, "seed", "https://source.example/item/1", null, 0);

        Assert.Equal(actor, item.CreatedBy);
        Assert.Equal(actor, revision.Actor);
        Assert.Equal("https://source.example/item/1", revision.SourceReference);
    }

    [Fact]
    public void REV_001_BR_006_substantive_change_creates_new_revision_and_keeps_prior_immutable()
    {
        var (tenant, project, actor) = Seed();
        var item = CreateItem(tenant, project, actor);
        var first = item.AddRevision("1.0", "{\"n\":1}", actor, "seed", null, null, 0);
        var second = item.AddRevision("1.0", "{\"n\":2}", actor, "change", null, null, 1);

        // The prior revision is fully intact: number, schema and content are unchanged.
        Assert.Equal(1, first.RevisionNumber);
        Assert.Equal("{\"n\":1}", first.Content);
        Assert.Equal("1.0", first.SchemaVersion);
        Assert.Equal(2, second.RevisionNumber);
        Assert.Equal("{\"n\":2}", second.Content);

        // The pointer advanced to the newest revision.
        Assert.Equal(second.Id, item.CurrentRevisionId);
        Assert.Equal(2, item.CurrentRevisionNumber);
    }

    [Fact]
    public void BR_006_revision_number_is_monotonic_and_starts_at_one()
    {
        var (tenant, project, actor) = Seed();
        var item = CreateItem(tenant, project, actor);

        var r1 = item.AddRevision("1.0", "a", actor, "one", null, null, 0);
        var r2 = item.AddRevision("1.0", "b", actor, "two", null, null, 1);
        var r3 = item.AddRevision("1.0", "c", actor, "three", null, null, 2);

        Assert.Equal(1, r1.RevisionNumber);
        Assert.Equal(2, r2.RevisionNumber);
        Assert.Equal(3, r3.RevisionNumber);
        Assert.Equal(3, item.CurrentRevisionNumber);
    }

    [Fact]
    public void REV_004_stale_expected_revision_number_yields_conflict()
    {
        var (tenant, project, actor) = Seed();
        var item = CreateItem(tenant, project, actor);
        item.AddRevision("1.0", "a", actor, "one", null, null, 0);

        var ex = Assert.Throws<StaleRevisionException>(() =>
            item.AddRevision("1.0", "b", actor, "two", null, null, 0));

        Assert.Equal(0, ex.ExpectedRevisionNumber);
        Assert.Equal(1, ex.CurrentRevisionNumber);

        // A stale write never advanced the pointer.
        Assert.Equal(1, item.CurrentRevisionNumber);
    }

    [Fact]
    public void REV_008_correction_records_superseded_revision_id_and_reason()
    {
        var (tenant, project, actor) = Seed();
        var item = CreateItem(tenant, project, actor);
        var original = item.AddRevision("1.0", "{\"v\":\"wrong\"}", actor, "capture", null, null, 0);

        var correction = item.AddRevision(
            "1.0", "{\"v\":\"correct\"}", actor, "correction to title", "src-2", original.Id, 1);

        Assert.Equal("correction to title", correction.Reason);
        Assert.Equal(original.Id, correction.SupersededRevisionId);
        Assert.Equal(item.Id, correction.KnowledgeItemId);
    }

    [Fact]
    public void BR_005_a_committed_revision_retains_its_schema_version_when_a_new_schema_arrives()
    {
        var (tenant, project, actor) = Seed();
        var item = CreateItem(tenant, project, actor);
        var v1 = item.AddRevision("1.0", "{}", actor, "seed", null, null, 0);
        var v2 = item.AddRevision("2.0", "{}", actor, "schema migration", null, null, 1);

        Assert.Equal("1.0", v1.SchemaVersion);
        Assert.Equal("2.0", v2.SchemaVersion);
        Assert.Equal(v2.Id, item.CurrentRevisionId);
        Assert.Equal(2, item.CurrentRevisionNumber);
    }

    [Fact]
    public void BR_009_info_states_are_distinct_and_follow_the_only_legal_transitions()
    {
        var (tenant, project, actor) = Seed();

        var proposed = CreateItem(tenant, project, actor);
        Assert.Equal(InfoState.Proposed, proposed.State);

        proposed.Approve();
        Assert.Equal(InfoState.Approved, proposed.State);
        Assert.Equal(ReviewState.Reviewed, proposed.ReviewState);

        proposed.Verify(hasQualifyingEvidence: true);
        Assert.Equal(InfoState.Verified, proposed.State);
        Assert.Equal(VerificationState.Verified, proposed.VerificationState);
    }

    [Fact]
    public void BR_009_reject_is_a_terminal_proposed_transition_and_verification_requires_approval()
    {
        var (tenant, project, actor) = Seed();

        var rejected = CreateItem(tenant, project, actor);
        rejected.Reject();
        Assert.Equal(InfoState.Rejected, rejected.State);
        Assert.Throws<InvalidOperationException>(() => rejected.Verify(hasQualifyingEvidence: true));
        Assert.Throws<InvalidOperationException>(() => rejected.Approve());

        // Verification requires an explicit approval first (BR-009: distinct states), even when
        // qualifying evidence is present.
        var proposed = CreateItem(tenant, project, actor);
        Assert.Throws<InvalidOperationException>(() => proposed.Verify(hasQualifyingEvidence: true));
    }

    [Fact]
    public void KNW_012_lifecycle_review_verification_and_currency_are_independent_dimensions()
    {
        var (tenant, project, actor) = Seed();
        var item = CreateItem(tenant, project, actor);
        item.AddRevision("1.0", "{}", actor, "seed", null, null, 0);

        // Approval advances the review dimension while verification stays untouched: an item can be
        // active, approved, unverified and current at the same time (KNW-012).
        item.Approve();
        Assert.Equal(InfoState.Approved, item.State);
        Assert.Equal(ReviewState.Reviewed, item.ReviewState);
        Assert.Equal(VerificationState.Unverified, item.VerificationState);
        Assert.Equal(CurrencyState.Current, item.CurrencyState);

        // A claim without qualifying evidence remains unverified (PRO-005, AC-08), and the refusal
        // leaves every dimension unchanged.
        Assert.Throws<InvalidOperationException>(() => item.Verify(hasQualifyingEvidence: false));
        Assert.Equal(InfoState.Approved, item.State);
        Assert.Equal(VerificationState.Unverified, item.VerificationState);

        // Only qualifying, policy-reviewed evidence advances verification.
        item.Verify(hasQualifyingEvidence: true);
        Assert.Equal(InfoState.Verified, item.State);
        Assert.Equal(VerificationState.Verified, item.VerificationState);
        Assert.Equal(ReviewState.Reviewed, item.ReviewState);
    }

    [Fact]
    public void KNW_012_a_new_revision_invalidates_prior_approval_and_verification()
    {
        var (tenant, project, actor) = Seed();
        var item = CreateItem(tenant, project, actor);
        item.AddRevision("1.0", "{}", actor, "seed", null, null, 0);
        item.Approve();
        item.Verify(hasQualifyingEvidence: true);
        Assert.Equal(InfoState.Verified, item.State);

        // New content is unreviewed, unverified content: prior approval/verification can never be
        // inherited (KNW-012, PRO-005, AC-08 — revision-specific evidence invalidation).
        item.AddRevision("1.0", "{\"v\":2}", actor, "change", null, null, 1);

        Assert.Equal(InfoState.Proposed, item.State);
        Assert.Equal(ReviewState.Pending, item.ReviewState);
        Assert.Equal(VerificationState.Unverified, item.VerificationState);
        Assert.Equal(CurrencyState.Current, item.CurrencyState);
    }

    [Fact]
    public void BR_009_duplicate_and_illegal_transitions_are_rejected()
    {
        var (tenant, project, actor) = Seed();

        var item = CreateItem(tenant, project, actor);
        item.Approve();
        Assert.Equal(InfoState.Approved, item.State);
        Assert.Throws<InvalidOperationException>(() => item.Approve());
        Assert.Throws<InvalidOperationException>(() => item.Reject());
    }

    [Fact]
    public void creation_guards_required_envelope_values()
    {
        var (tenant, project, actor) = Seed();

        Assert.Throws<ArgumentException>(() =>
            KnowledgeItem.Create(tenant, project, " ", "Title", Classification.Public, RepresentationKind.Text, "t", "1", actor));
        Assert.Throws<ArgumentException>(() =>
            KnowledgeItem.Create(tenant, project, "k", " ", Classification.Public, RepresentationKind.Text, "t", "1", actor));
        Assert.Throws<ArgumentException>(() =>
            KnowledgeItem.Create(tenant, project, "k", "Title", Classification.Public, RepresentationKind.Text, " ", "1", actor));
        Assert.Throws<ArgumentException>(() =>
            KnowledgeItem.Create(tenant, project, "k", "Title", Classification.Public, RepresentationKind.Text, "t", " ", actor));
    }

    [Fact]
    public void revision_guards_required_values_and_the_minimum_revision_number()
    {
        var (tenant, project, actor) = Seed();
        var item = CreateItem(tenant, project, actor);

        Assert.Throws<ArgumentException>(() => item.AddRevision(" ", "{}", actor, "r", null, null, 0));
        Assert.Throws<ArgumentException>(() => item.AddRevision("1.0", " ", actor, "r", null, null, 0));
        Assert.Throws<ArgumentException>(() => item.AddRevision("1.0", "{}", actor, " ", null, null, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            KnowledgeRevision.Create(tenant, item.Id, 0, "1.0", "{}", actor, "r", null, null));
    }

    [Fact]
    public void REV_001_revisions_are_immutable_with_no_mutable_surface()
    {
        var writable = typeof(KnowledgeRevision)
            .GetProperties()
            .Where(p => p.CanWrite)
            .ToArray();

        Assert.Empty(writable);
    }

    [Fact]
    public void KNW_012_approval_is_bound_to_the_revision_that_was_reviewed()
    {
        var (tenant, project, actor) = Seed();
        var item = CreateItem(tenant, project, actor);
        var first = item.AddRevision("1.0", "{\"v\":1}", actor, "first", null, null, 0);

        item.Approve(first.Id);

        Assert.Equal(InfoState.Approved, item.State);
        Assert.Equal(ReviewState.Reviewed, item.ReviewState);
    }

    [Fact]
    public void KNW_012_review_of_a_superseded_revision_is_refused_and_changes_nothing()
    {
        var (tenant, project, actor) = Seed();
        var item = CreateItem(tenant, project, actor);
        var first = item.AddRevision("1.0", "{\"v\":1}", actor, "first", null, null, 0);
        var second = item.AddRevision("1.0", "{\"v\":2}", actor, "changed while under review", null, null, 1);

        var approve = Assert.Throws<StaleReviewException>(() => item.Approve(first.Id));
        Assert.Equal(first.Id, approve.ReviewedRevisionId);
        Assert.Equal(second.Id, approve.CurrentRevisionId);
        Assert.Throws<StaleReviewException>(() => item.Reject(first.Id));

        Assert.Equal(InfoState.Proposed, item.State);
        Assert.Equal(ReviewState.Pending, item.ReviewState);
    }

    [Fact]
    public void KNW_012_an_item_without_a_revision_cannot_be_reviewed()
    {
        var (tenant, project, actor) = Seed();
        var item = CreateItem(tenant, project, actor);

        Assert.Throws<InvalidOperationException>(() => item.Approve(KnowledgeRevisionId.New()));
        Assert.Throws<InvalidOperationException>(() => item.Reject(KnowledgeRevisionId.New()));
        Assert.Equal(InfoState.Proposed, item.State);
    }

    private static (TenantId Tenant, ProjectId Project, PrincipalId Actor) Seed() =>
        (TenantId.New(), ProjectId.New(), PrincipalId.New());

    private static KnowledgeItem CreateItem(
        TenantId? tenant = null,
        ProjectId? project = null,
        PrincipalId? actor = null,
        string key = "api-design",
        Classification classification = Classification.Internal,
        RepresentationKind representation = RepresentationKind.Text,
        string typeId = "doc.note",
        string schemaVersion = "1.0")
    {
        return KnowledgeItem.Create(
            tenant ?? TenantId.New(),
            project ?? ProjectId.New(),
            key,
            "API Design Notes",
            classification,
            representation,
            typeId,
            schemaVersion,
            actor ?? PrincipalId.New());
    }
}