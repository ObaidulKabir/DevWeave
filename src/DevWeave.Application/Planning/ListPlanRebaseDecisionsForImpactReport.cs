using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Planning;

public sealed class ListPlanRebaseDecisionsForImpactReport
{
    private readonly IImpactAnalysisReportRepository _reports;
    private readonly IPlanRebaseDecisionRepository _decisions;

    public ListPlanRebaseDecisionsForImpactReport(
        IImpactAnalysisReportRepository reports,
        IPlanRebaseDecisionRepository decisions)
    {
        _reports = reports;
        _decisions = decisions;
    }

    public async Task<IReadOnlyList<PlanRebaseDecision>> ExecuteAsync(
        TenantId tenantId,
        ProjectId projectId,
        ImpactAnalysisReportId impactReportId,
        CancellationToken ct = default)
    {
        var report = await _reports.GetByIdAsync(tenantId, impactReportId, ct)
            ?? throw new NotFoundException($"Impact analysis report '{impactReportId}' was not found.");
        if (report.ProjectId != projectId)
            throw new NotFoundException($"Impact analysis report '{impactReportId}' was not found.");

        return await _decisions.ListByImpactReportAsync(tenantId, impactReportId, ct);
    }
}
