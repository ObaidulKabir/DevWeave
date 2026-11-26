using DevWeave.Application.Projects;
using DevWeave.Domain.Activities;
using DevWeave.Domain.Evidence;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Relations;
using EvidenceRecord = DevWeave.Domain.Evidence.Evidence;

namespace DevWeave.Application.Tests.Projects;

public sealed class ClosedLoopAnalyticsTests
{
    [Fact]
    public void Reported_completion_alone_does_not_increase_evidence_backed_completion()
    {
        var graph = NewGraph();
        var work = WorkItem.Create(
            graph.Tenant.Id, graph.Project.Id, "W-1", "Ship checkout", "Deliver the checkout slice",
            null, null, null, WorkPriority.Normal, graph.Actor.Id);
        work.Report(ReportedExecutionState.Completed, graph.Actor.Id, "reported done", 1);

        var before = Value(ClosedLoopAnalytics.Compute(Population(graph, workItems: [work])), "closedloop.evidence_backed_completion");

        var extra = WorkItem.Create(
            graph.Tenant.Id, graph.Project.Id, "W-2", "Ship billing", "Deliver billing",
            null, null, null, WorkPriority.Normal, graph.Actor.Id);
        extra.Report(ReportedExecutionState.Completed, graph.Actor.Id, "reported done", 1);

        var after = Value(ClosedLoopAnalytics.Compute(Population(graph, workItems: [work, extra])), "closedloop.evidence_backed_completion");

        Assert.Equal(0m, before);
        Assert.Equal(0m, after);
    }

    [Fact]
    public void Revising_a_requirement_makes_prior_evidence_stale()
    {
        var graph = NewGraph();
        var item = Requirement(graph, "FR-1", "Checkout");
        var revision = item.AddRevision("1.0", "{\"statement\":\"v1\"}", graph.Actor.Id, "initial", null, null, 0);
        item.Approve();
        var evidence = AcceptedEvidence(graph, item, revision);

        var current = ClosedLoopAnalytics.Compute(Population(graph, items: [item], revisions: [revision], evidence: [evidence]));
        Assert.Equal(1m, Value(current, "closedloop.requirement_verification_coverage"));
        Assert.Equal(1m, Value(current, "closedloop.reliability"));

        var revised = item.AddRevision("1.0", "{\"statement\":\"v2\"}", graph.Actor.Id, "tighten", null, revision.Id, 1);
        item.Approve();
        var stale = ClosedLoopAnalytics.Compute(Population(
            graph,
            items: [item],
            revisions: [revision, revised],
            evidence: [evidence]));

        Assert.Equal(0m, Value(stale, "closedloop.requirement_verification_coverage"));
        Assert.Equal(0m, Value(stale, "closedloop.reliability"));
    }

    [Fact]
    public void Hiding_a_requirement_changes_numerator_and_denominator()
    {
        var graph = NewGraph();
        var visible = ApprovedRequirement(graph, "FR-1", "Checkout");
        var hidden = ApprovedRequirement(graph, "FR-2", "Billing");
        var visibleEvidence = AcceptedEvidence(graph, visible.Item, visible.Revision);
        var hiddenEvidence = AcceptedEvidence(graph, hidden.Item, hidden.Revision);

        var all = ClosedLoopAnalytics.Compute(Population(
            graph,
            items: [visible.Item, hidden.Item],
            revisions: [visible.Revision, hidden.Revision],
            evidence: [visibleEvidence, hiddenEvidence]));
        var filtered = ClosedLoopAnalytics.Compute(Population(
            graph,
            items: [visible.Item],
            revisions: [visible.Revision],
            evidence: [visibleEvidence]));

        Assert.Equal(1m, Value(all, "closedloop.requirement_verification_coverage"));
        Assert.Equal(1m, Value(filtered, "closedloop.requirement_verification_coverage"));
        Assert.Contains("Numerator 2, denominator 2", PopulationText(all, "closedloop.requirement_verification_coverage"));
        Assert.Contains("Numerator 1, denominator 1", PopulationText(filtered, "closedloop.requirement_verification_coverage"));
    }

    [Fact]
    public void Quality_dimensions_stay_separate()
    {
        var graph = NewGraph();
        var requirement = ApprovedRequirement(graph, "FR-1", "Checkout");
        var metrics = ClosedLoopAnalytics.Compute(Population(
            graph,
            items: [requirement.Item],
            revisions: [requirement.Revision],
            agentActivities: 2,
            agentActivitiesWithMetadata: 1));

        Assert.Equal(0m, Value(metrics, "closedloop.requirement_verification_coverage"));
        Assert.Equal(1m, Value(metrics, "closedloop.completeness"));
        Assert.Equal(0.5m, Value(metrics, "closedloop.provenance_quality"));
        Assert.NotEqual(
            Value(metrics, "closedloop.completeness"),
            Value(metrics, "closedloop.provenance_quality"));
    }

    [Fact]
    public void An_active_satisfies_edge_is_traceability_and_a_revoked_edge_is_not()
    {
        var graph = NewGraph();
        var (requirement, _) = ApprovedRequirement(graph, "FR-1", "Checkout");
        var decision = KnowledgeItem.Create(
            graph.Tenant.Id, graph.Project.Id, "ADR-1", "Checkout decision", Classification.Internal,
            RepresentationKind.StructuredData, "architecture.decision", "1.0", graph.Actor.Id);
        var edge = Relationship.Create(
            graph.Tenant.Id, graph.Project.Id, decision.Id, requirement.Id, "satisfies", graph.Actor.Id,
            rationale: "The decision covers checkout");

        var traced = ClosedLoopAnalytics.Compute(Population(graph, items: [requirement, decision], relationships: [edge]));
        Assert.Equal(1m, Value(traced, "closedloop.traceability_coverage"));
        Assert.Contains("Numerator 1, denominator 1", PopulationText(traced, "closedloop.traceability_coverage"), StringComparison.Ordinal);

        edge.Revoke();
        var revoked = ClosedLoopAnalytics.Compute(Population(graph, items: [requirement, decision], relationships: [edge]));
        Assert.Equal(0m, Value(revoked, "closedloop.traceability_coverage"));
        Assert.Contains("Numerator 0, denominator 1", PopulationText(revoked, "closedloop.traceability_coverage"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("implements")]
    [InlineData("validates")]
    public void Implements_and_validates_edges_count_as_traceability(string relationType)
    {
        var graph = NewGraph();
        var (requirement, _) = ApprovedRequirement(graph, "FR-1", "Checkout");
        var source = KnowledgeItem.Create(
            graph.Tenant.Id, graph.Project.Id, "SRC-1", "Checkout source", Classification.Internal,
            RepresentationKind.StructuredData, "architecture.component", "1.0", graph.Actor.Id);
        var edge = Relationship.Create(
            graph.Tenant.Id, graph.Project.Id, source.Id, requirement.Id, relationType, graph.Actor.Id,
            rationale: "The source covers checkout");

        var traced = ClosedLoopAnalytics.Compute(Population(graph, items: [requirement, source], relationships: [edge]));
        Assert.Equal(1m, Value(traced, "closedloop.traceability_coverage"));

        edge.Revoke();
        var revoked = ClosedLoopAnalytics.Compute(Population(graph, items: [requirement, source], relationships: [edge]));
        Assert.Equal(0m, Value(revoked, "closedloop.traceability_coverage"));
    }

    [Fact]
    public void A_current_revision_outside_its_valid_interval_is_not_fresh()
    {
        // A revision with no ValidFrom is valid from RecordedAt (wall-clock UtcNow). The as-of
        // has to stay after that, or the current revision is not inside its interval yet.
        var graph = NewGraph() with { Now = DateTimeOffset.UtcNow.AddMinutes(5) };
        var staleItem = Requirement(graph, "FR-1", "Checkout");
        var stale = staleItem.AddRevision(
            "1.0", "{\"statement\":\"old\"}", graph.Actor.Id, "initial", null, null, 0,
            validFrom: graph.Now.AddDays(-10), validTo: graph.Now.AddDays(-1));
        var currentItem = Requirement(graph, "FR-2", "Billing");
        var current = currentItem.AddRevision("1.0", "{\"statement\":\"now\"}", graph.Actor.Id, "initial", null, null, 0);

        var metrics = ClosedLoopAnalytics.Compute(Population(
            graph, items: [staleItem, currentItem], revisions: [stale, current]));

        Assert.Equal(0.5m, Value(metrics, "closedloop.freshness"));
        Assert.Contains("Numerator 1, denominator 2", PopulationText(metrics, "closedloop.freshness"), StringComparison.Ordinal);
    }

    [Fact]
    public void Blocked_work_is_not_actionable_and_cancelled_work_is_excluded()
    {
        var graph = NewGraph();
        var planned = WorkItem.Create(
            graph.Tenant.Id, graph.Project.Id, "W-1", "Ship checkout", "Deliver checkout",
            null, null, null, WorkPriority.Normal, graph.Actor.Id);
        var blocked = WorkItem.Create(
            graph.Tenant.Id, graph.Project.Id, "W-2", "Ship billing", "Deliver billing",
            null, null, null, WorkPriority.Normal, graph.Actor.Id);
        blocked.ChangePlannedState(PlannedWorkState.Ready, graph.Actor.Id, "ready", 1);
        blocked.ChangePlannedState(PlannedWorkState.InProgress, graph.Actor.Id, "start", 2);
        blocked.ChangePlannedState(PlannedWorkState.Blocked, graph.Actor.Id, "waiting", 3);
        var cancelled = WorkItem.Create(
            graph.Tenant.Id, graph.Project.Id, "W-3", "Drop tax", "Drop the tax slice",
            null, null, null, WorkPriority.Normal, graph.Actor.Id);
        cancelled.ChangePlannedState(PlannedWorkState.Cancelled, graph.Actor.Id, "drop", 1);

        var metrics = ClosedLoopAnalytics.Compute(Population(graph, workItems: [planned, blocked, cancelled]));

        Assert.Equal(0.5m, Value(metrics, "closedloop.actionability"));
        Assert.Contains("Numerator 1, denominator 2", PopulationText(metrics, "closedloop.actionability"), StringComparison.Ordinal);
    }

    private static decimal Value(IReadOnlyList<DevWeave.Contracts.Planned.V1.MetricView> metrics, string key) =>
        Assert.Single(metrics, metric => metric.Key == key).Value;

    private static string PopulationText(IReadOnlyList<DevWeave.Contracts.Planned.V1.MetricView> metrics, string key) =>
        Assert.Single(metrics, metric => metric.Key == key).IncludedPopulation;

    private static ClosedLoopPopulation Population(
        Graph graph,
        IReadOnlyList<KnowledgeItem>? items = null,
        IReadOnlyList<KnowledgeRevision>? revisions = null,
        IReadOnlyList<Relationship>? relationships = null,
        IReadOnlyList<EvidenceRecord>? evidence = null,
        IReadOnlyList<WorkItem>? workItems = null,
        int agentActivities = 0,
        int agentActivitiesWithMetadata = 0) =>
        new(
            items ?? [],
            revisions ?? [],
            relationships ?? [],
            evidence ?? [],
            workItems ?? [],
            agentActivities,
            agentActivitiesWithMetadata,
            graph.Now);

    private static (KnowledgeItem Item, KnowledgeRevision Revision) ApprovedRequirement(Graph graph, string key, string title)
    {
        var item = Requirement(graph, key, title);
        var revision = item.AddRevision("1.0", "{\"statement\":\"" + title + "\"}", graph.Actor.Id, "initial", null, null, 0);
        item.Approve();
        return (item, revision);
    }

    private static KnowledgeItem Requirement(Graph graph, string key, string title) =>
        KnowledgeItem.Create(
            graph.Tenant.Id,
            graph.Project.Id,
            key,
            title,
            Classification.Internal,
            RepresentationKind.StructuredData,
            "requirements.functional",
            "1.0",
            graph.Actor.Id);

    private static EvidenceRecord AcceptedEvidence(Graph graph, KnowledgeItem item, KnowledgeRevision revision)
    {
        var evidence = EvidenceRecord.Create(
            graph.Tenant.Id,
            graph.Project.Id,
            ActivityId.New(),
            "test-run",
            "sha256:abc",
            null,
            item.Id,
            revision.Id,
            item.Title);
        evidence.Review(EvidenceReviewState.Accepted, graph.Actor.Id);
        return evidence;
    }

    private static Graph NewGraph()
    {
        var tenant = Tenant.Create("Loop Corp");
        var actor = Principal.Create(tenant.Id, "owner@loop.test", PrincipalType.Human);
        var project = Project.Create(tenant.Id, "Checkout", "checkout", null, LifecycleProfileId.New(), actor.Id);
        return new Graph(tenant, actor, project, DateTimeOffset.Parse("2026-11-23T00:00:00Z"));
    }

    private sealed record Graph(Tenant Tenant, Principal Actor, Project Project, DateTimeOffset Now);
}
