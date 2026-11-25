using DevWeave.Contracts.Planned.V1;
using DevWeave.Domain.Activities;
using DevWeave.Domain.Evidence;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Relations;
using EvidenceRecord = DevWeave.Domain.Evidence.Evidence;

namespace DevWeave.Application.Projects;

/// <summary>
/// Authorized inputs for closed-loop metrics. Callers must already have removed knowledge the
/// caller cannot see. Hidden items must not appear in items, relationships, evidence, or revisions.
/// </summary>
public sealed record ClosedLoopPopulation(
    IReadOnlyList<KnowledgeItem> Items,
    IReadOnlyList<KnowledgeRevision> Revisions,
    IReadOnlyList<Relationship> Relationships,
    IReadOnlyList<EvidenceRecord> Evidence,
    IReadOnlyList<WorkItem> WorkItems,
    int AgentActivities,
    int AgentActivitiesWithMetadata,
    DateTimeOffset AsOf);

/// <summary>
/// Versioned closed-loop measures. Each dimension stays a separate metric. None of them is a
/// blend of the others, and reported completion is not an input to evidence-backed completion.
/// </summary>
public static class ClosedLoopAnalytics
{
    public const string DefinitionVersion = "closed-loop.1.0";

    private static readonly HashSet<string> RequirementTypes = new(StringComparer.Ordinal)
    {
        "requirements.functional",
        "requirements.nonfunctional",
        "requirements.constraint",
        "plan.requirement",
        "plan.constraint",
        "plan.acceptance",
    };

    private static readonly HashSet<string> TraceTypes = new(StringComparer.Ordinal)
    {
        RelationTypeCodes.Satisfies,
        RelationTypeCodes.Implements,
        RelationTypeCodes.Validates,
    };

    public static IReadOnlyList<MetricView> Compute(ClosedLoopPopulation population)
    {
        ArgumentNullException.ThrowIfNull(population);
        var now = population.AsOf;
        var requirements = population.Items.Where(IsRequirement).ToArray();
        var approved = requirements.Where(IsApproved).ToArray();
        var currentByItem = population.Items
            .Where(item => item.CurrentRevisionId is not null)
            .ToDictionary(item => item.Id, item => item.CurrentRevisionId!);

        var verifiedRequirements = approved.Count(item => HasCurrentEvidence(population.Evidence, item));
        var traced = approved.Count(item => HasTrace(population.Relationships, item));
        var reportedComplete = population.WorkItems
            .Where(item => item.ReportedState == ReportedExecutionState.Completed)
            .ToArray();
        var evidenceBacked = reportedComplete.Count(item => item.VerificationState == WorkVerificationState.Verified);

        var withRevision = requirements.Count(item => item.CurrentRevisionId is not null);
        var accepted = population.Evidence
            .Where(record => record.ReviewState == EvidenceReviewState.Accepted && record.CriterionItemId is not null)
            .ToArray();
        var reliable = accepted.Count(record =>
            currentByItem.TryGetValue(record.CriterionItemId!, out var current) &&
            record.CriterionRevisionId == current);

        var revisionById = population.Revisions.ToDictionary(revision => revision.Id);
        var fresh = population.Items.Count(item =>
            item.CurrentRevisionId is { } revisionId &&
            revisionById.TryGetValue(revisionId, out var revision) &&
            revision.IsValidAt(now));
        var revisionedItems = population.Items.Count(item => item.CurrentRevisionId is not null);

        var openWork = population.WorkItems
            .Where(item => item.PlannedState != PlannedWorkState.Cancelled)
            .ToArray();
        var actionable = openWork.Count(item =>
            item.PlannedState is PlannedWorkState.Planned or PlannedWorkState.Ready or PlannedWorkState.InProgress);

        return
        [
            Metric(
                "closedloop.requirement_verification_coverage",
                verifiedRequirements,
                approved.Length,
                now,
                "Approved requirements in the authorized view with accepted evidence pinned to the current revision",
                "Unapproved requirements, and evidence pinned to a superseded revision, are outside the numerator"),
            Metric(
                "closedloop.traceability_coverage",
                traced,
                approved.Length,
                now,
                "Approved requirements with an active satisfies, implements, or validates edge",
                "Requirements without one of those edges, and revoked edges, are outside the numerator"),
            Metric(
                "closedloop.evidence_backed_completion",
                evidenceBacked,
                reportedComplete.Length,
                now,
                "Reported-complete work items independently verified",
                "Reported completion without verification does not enter the numerator"),
            Metric(
                "closedloop.completeness",
                withRevision,
                requirements.Length,
                now,
                "Requirement items in the authorized view that have a current revision",
                "Requirement items with no revision"),
            Metric(
                "closedloop.reliability",
                reliable,
                accepted.Length,
                now,
                "Accepted evidence whose criterion pin equals that item's current revision",
                "Accepted evidence pinned to a superseded revision"),
            Metric(
                "closedloop.provenance_quality",
                population.AgentActivitiesWithMetadata,
                population.AgentActivities,
                now,
                "Generated activities that have agent execution metadata",
                "Generated activities with no model or configuration record"),
            Metric(
                "closedloop.freshness",
                fresh,
                revisionedItems,
                now,
                "Items whose current revision is inside its valid-time interval at as-of",
                "Current revisions whose valid interval does not cover as-of"),
            Metric(
                "closedloop.actionability",
                actionable,
                openWork.Length,
                now,
                "Non-cancelled work items still in Planned, Ready, or InProgress",
                "Cancelled work is excluded from both sides; blocked work is not actionable"),
        ];

        static bool IsRequirement(KnowledgeItem item) => RequirementTypes.Contains(item.TypeId);

        static bool IsApproved(KnowledgeItem item) =>
            item.State is InfoState.Approved or InfoState.Verified;
    }

    private static bool HasCurrentEvidence(IReadOnlyList<EvidenceRecord> evidence, KnowledgeItem item) =>
        item.CurrentRevisionId is { } current &&
        evidence.Any(record => record.IsCurrentAcceptedVerification(current) && record.CriterionItemId == item.Id);

    private static bool HasTrace(IReadOnlyList<Relationship> relationships, KnowledgeItem item) =>
        relationships.Any(edge =>
            edge.Status == RelationshipStatus.Active &&
            edge.TargetItemId == item.Id &&
            TraceTypes.Contains(edge.RelationTypeCode));

    private static MetricView Metric(
        string key,
        int numerator,
        int denominator,
        DateTimeOffset asOf,
        string population,
        string exclusions) =>
        new(
            key,
            denominator == 0 ? 0m : decimal.Round((decimal)numerator / denominator, 4, MidpointRounding.AwayFromZero),
            "ratio",
            DefinitionVersion,
            asOf,
            $"{population}. Numerator {numerator}, denominator {denominator}.",
            exclusions,
            false);
}
