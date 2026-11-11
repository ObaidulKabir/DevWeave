using DevWeave.Application.Abstractions;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Contracts.Planned.V1;
using DevWeave.Domain.Knowledge;

namespace DevWeave.Application.Projects;

public sealed class ProjectReporting : IProjectReporting
{
    private readonly IProjectRepository _projects;
    private readonly IKnowledgeItemRepository _knowledgeItems;
    private readonly IRelationshipRepository _relationships;
    private readonly IAuditJournal _auditJournal;

    public ProjectReporting(
        IProjectRepository projects,
        IKnowledgeItemRepository knowledgeItems,
        IRelationshipRepository relationships,
        IAuditJournal auditJournal)
    {
        _projects = projects;
        _knowledgeItems = knowledgeItems;
        _relationships = relationships;
        _auditJournal = auditJournal;
    }

    public async Task<IReadOnlyList<MetricView>> GetMetricsAsync(AuthorizedScope scope,
        DateTimeOffset asOf, CancellationToken ct = default)
    {
        if (scope.ProjectId is null)
            throw new ArgumentException("Project ID is required for reporting.");

        var items = await _knowledgeItems.ListByProjectAsync(scope.TenantId, scope.ProjectId, ct);
        var relationships = await _relationships.ListByProjectAsync(scope.TenantId, scope.ProjectId, ct);

        var totalItems = items.Count;
        var approvedItems = items.Count(i => i.State == InfoState.Approved || i.State == InfoState.Verified);
        var verifiedItems = items.Count(i => i.VerificationState == VerificationState.Verified);
        
        var metrics = new List<MetricView>
        {
            new("knowledge.total_items", totalItems, "count", "1.0", DateTimeOffset.UtcNow, "Project items", ""),
            new("knowledge.approved_items", approvedItems, "count", "1.0", DateTimeOffset.UtcNow, "Project items", ""),
            new("knowledge.verified_items", verifiedItems, "count", "1.0", DateTimeOffset.UtcNow, "Project items", ""),
            new("knowledge.verification_coverage", totalItems > 0 ? (decimal)verifiedItems / totalItems : 0, "ratio", "1.0", DateTimeOffset.UtcNow, "Project items", ""),
            new("knowledge.relationship_count", relationships.Count, "count", "1.0", DateTimeOffset.UtcNow, "Project relationships", "")
        };

        return metrics;
    }

    public async Task<Guid> GenerateReportAsync(AuthorizedScope scope, string declaredPurpose,
        CancellationToken ct = default)
    {
        if (scope.ProjectId is null)
            throw new ArgumentException("Project ID is required for reporting.");

        // For MVP, we'll simulate report generation by logging the request.
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
