using DevWeave.Application.Abstractions;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Contracts.Planned.V1;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Lifecycle;

namespace DevWeave.Application.Projects;

public sealed class ProjectReporting : IProjectReporting
{
    private readonly IProjectRepository _projects;
    private readonly IKnowledgeItemRepository _knowledgeItems;
    private readonly IRelationshipRepository _relationships;
    private readonly IWorkPlanningRepository _work;
    private readonly IAuditJournal _auditJournal;

    public ProjectReporting(
        IProjectRepository projects,
        IKnowledgeItemRepository knowledgeItems,
        IRelationshipRepository relationships,
        IWorkPlanningRepository work,
        IAuditJournal auditJournal)
    {
        _projects = projects;
        _knowledgeItems = knowledgeItems;
        _relationships = relationships;
        _work = work;
        _auditJournal = auditJournal;
    }

    public async Task<IReadOnlyList<MetricView>> GetMetricsAsync(AuthorizedScope scope,
        DateTimeOffset asOf, CancellationToken ct = default)
    {
        if (scope.ProjectId is null)
            throw new ArgumentException("Project ID is required for reporting.");

        _ = await _projects.GetByIdAsync(scope.TenantId, scope.ProjectId, ct)
            ?? throw new NotFoundException($"Project '{scope.ProjectId}' was not found.");

        var items = await _knowledgeItems.ListByProjectAsync(scope.TenantId, scope.ProjectId, ct);
        var relationships = await _relationships.ListByProjectAsync(scope.TenantId, scope.ProjectId, ct);
        var workItems = await _work.ListWorkItemsAsync(scope.TenantId, scope.ProjectId, ct);

        var totalItems = items.Count;
        var approvedItems = items.Count(i => i.State == InfoState.Approved || i.State == InfoState.Verified);
        var verifiedItems = items.Count(i => i.VerificationState == VerificationState.Verified);

        var planned = workItems.Count(w => w.PlannedState == PlannedWorkState.Planned);
        var reportedCompleted = workItems.Count(w => w.ReportedState == ReportedExecutionState.Completed);
        var workVerified = workItems.Count(w => w.VerificationState == WorkVerificationState.Verified);

        var calculatedAt = DateTimeOffset.UtcNow;
        const string def = "1.0";

        var metrics = new List<MetricView>
        {
            new("knowledge.total_items", totalItems, "count", def, calculatedAt, "Project knowledge items", "", false),
            new("knowledge.approved_items", approvedItems, "count", def, calculatedAt, "Project knowledge items", "", false),
            new("knowledge.verified_items", verifiedItems, "count", def, calculatedAt, "Project knowledge items", "", false),
            new(
                "knowledge.verification_coverage",
                totalItems > 0 ? (decimal)verifiedItems / totalItems : 0,
                "ratio",
                def,
                calculatedAt,
                "Project knowledge items",
                "",
                false),
            new("knowledge.relationship_count", relationships.Count, "count", def, calculatedAt, "Project relationships", "", false),
            new("work.planned_count", planned, "count", def, calculatedAt, "Work items in Planned state", "Non-planned work items", false),
            new("work.reported_completed_count", reportedCompleted, "count", def, calculatedAt, "Work items reported Completed", "Incomplete work items", false),
            new("work.verified_count", workVerified, "count", def, calculatedAt, "Work items with Verified state", "Unverified work items", false),
            new(
                "work.completion_estimate",
                workItems.Count > 0 ? (decimal)reportedCompleted / workItems.Count : 0,
                "ratio",
                def,
                calculatedAt,
                "Reported-complete ÷ all work items (estimate)",
                "Does not imply verified completion",
                true),
        };

        return metrics;
    }

    public async Task<Guid> GenerateReportAsync(AuthorizedScope scope, string declaredPurpose,
        CancellationToken ct = default)
    {
        if (scope.ProjectId is null)
            throw new ArgumentException("Project ID is required for reporting.");

        var reportId = Guid.NewGuid();

        await _auditJournal.AppendAsync(
            scope,
            "ReportGenerated",
            $"Project:{scope.ProjectId.Value}",
            $"ReportId:{reportId};Purpose:{declaredPurpose}",
            ct);

        return reportId;
    }
}
