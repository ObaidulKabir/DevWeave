using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Tests.Projects;

public sealed class ProjectJournalAndBaselineTests
{
    [Fact]
    public void Project_event_preserves_typed_pinned_inputs_and_canonical_metadata()
    {
        var tenant = TenantId.New();
        var project = ProjectId.New();
        var actor = PrincipalId.New();
        var itemId = Guid.NewGuid();
        var revisionId = Guid.NewGuid();

        var recorded = ProjectEvent.Record(
            tenant,
            project,
            "requirements.revised",
            "1.0",
            actor,
            "Record an approved requirement revision",
            DateTimeOffset.Parse("2026-11-20T10:00:00Z"),
            baselineId: null,
            inputs: [ProjectResourceRef.Local(ProjectResourceKind.KnowledgeItem, itemId, revisionId)],
            outputs: [ProjectResourceRef.Local(ProjectResourceKind.KnowledgeRevision, revisionId)],
            metadata: new Dictionary<string, string>
            {
                ["status"] = "approved",
                ["changeKind"] = "threshold-tightening",
            },
            correlationId: "correlation-1",
            idempotencyKey: "requirement-revision-1",
            reason: "The latency threshold was tightened.",
            authorizationContext: "project-role:owner");

        var input = Assert.Single(recorded.Inputs);
        Assert.Equal(ProjectResourceKind.KnowledgeItem, input.Kind);
        Assert.Equal(itemId.ToString("D"), input.Identifier);
        Assert.Equal(revisionId.ToString("D"), input.RevisionIdentifier);
        Assert.Equal(0, input.Position);
        Assert.Equal(
            "{\"changeKind\":\"threshold-tightening\",\"status\":\"approved\"}",
            recorded.MetadataJson);
    }

    [Fact]
    public void Project_event_rejects_unbounded_metadata_and_duplicate_refs()
    {
        var resource = ProjectResourceRef.Local(ProjectResourceKind.WorkItem, Guid.NewGuid());

        Assert.Throws<ArgumentException>(() => ProjectEvent.Record(
            TenantId.New(),
            ProjectId.New(),
            "work.changed",
            "1.0",
            PrincipalId.New(),
            "Change work",
            DateTimeOffset.UtcNow,
            baselineId: null,
            inputs: [resource, resource],
            outputs: [],
            metadata: null,
            correlationId: "correlation",
            idempotencyKey: "idempotency",
            reason: "Changed",
            authorizationContext: "project-role:owner"));

        Assert.Throws<ArgumentException>(() => ProjectEvent.Record(
            TenantId.New(),
            ProjectId.New(),
            "work.changed",
            "1.0",
            PrincipalId.New(),
            "Change work",
            DateTimeOffset.UtcNow,
            baselineId: null,
            inputs: [],
            outputs: [resource],
            metadata: new Dictionary<string, string> { ["arbitrarySecret"] = "not allowed" },
            correlationId: "correlation",
            idempotencyKey: "idempotency",
            reason: "Changed",
            authorizationContext: "project-role:owner"));
    }

    [Fact]
    public void Baseline_v2_checksum_is_order_independent_but_context_sensitive()
    {
        var id = BaselineId.New();
        var tenant = TenantId.New();
        var project = ProjectId.New();
        var profile = Guid.NewGuid();
        var firstRevision = new BaselineRevisionRef(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "requirements.functional",
            "1.0",
            99);
        var secondRevision = new BaselineRevisionRef(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "requirements.nonfunctional",
            "1.0",
            42);
        var edge = new BaselineSemanticEdge(
            Guid.NewGuid(),
            firstRevision.ItemId,
            secondRevision.ItemId,
            "depends_on",
            17);

        var first = PlanBaselineV2.CreateExact(
            id,
            tenant,
            project,
            [firstRevision, secondRevision],
            [edge],
            [],
            [new BaselineCriterionRef(firstRevision.ItemId, firstRevision.RevisionId, 4)],
            [],
            ["Response time is below 200ms"],
            profile,
            "1.0",
            "baseline-policy/2.0",
            predecessorBaselineId: null);
        var reordered = PlanBaselineV2.CreateExact(
            id,
            tenant,
            project,
            [secondRevision, firstRevision],
            [edge],
            [],
            [new BaselineCriterionRef(firstRevision.ItemId, firstRevision.RevisionId, 0)],
            [],
            ["Response time is below 200ms"],
            profile,
            "1.0",
            "baseline-policy/2.0",
            predecessorBaselineId: null);
        var changedPolicy = PlanBaselineV2.CreateExact(
            id,
            tenant,
            project,
            [firstRevision, secondRevision],
            [edge],
            [],
            [new BaselineCriterionRef(firstRevision.ItemId, firstRevision.RevisionId, 0)],
            [],
            ["Response time is below 200ms"],
            profile,
            "1.0",
            "baseline-policy/2.1",
            predecessorBaselineId: null);

        Assert.Equal(first.ManifestChecksum, reordered.ManifestChecksum);
        Assert.NotEqual(first.ManifestChecksum, changedPolicy.ManifestChecksum);
        Assert.True(first.HasValidChecksum());
        Assert.Equal(BaselineHistoricalPrecision.Exact, first.HistoricalPrecision);
        Assert.Equal(
            new[] { 0, 1 },
            first.ApprovedRevisions.Select(reference => reference.Position).ToArray());
    }

    [Fact]
    public void Legacy_baseline_projection_explicitly_marks_reduced_precision()
    {
        var legacy = PlanBaseline.Lock(
            TenantId.New(),
            ProjectId.New(),
            "Legacy goal",
            [Guid.NewGuid()],
            [],
            ["A legacy free-text criterion"],
            expectedPlanVersion: 1);

        var projected = PlanBaselineV2.ProjectLegacy(
            legacy,
            Guid.NewGuid(),
            "legacy",
            "baseline-policy/2.0",
            predecessorBaselineId: null);

        Assert.Equal(BaselineHistoricalPrecision.ReducedLegacy, projected.HistoricalPrecision);
        Assert.Equal(Guid.Empty, Assert.Single(projected.ApprovedRevisions).ItemId);
        Assert.True(projected.HasValidChecksum());
    }
}
