using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Planning;

public sealed class GetImpactAnalysisReport
{
    private readonly IImpactAnalysisReportRepository _reports;

    public GetImpactAnalysisReport(IImpactAnalysisReportRepository reports)
    {
        _reports = reports;
    }

    public async Task<ImpactAnalysisReport> ExecuteAsync(
        TenantId tenantId,
        ProjectId projectId,
        ImpactAnalysisReportId id,
        CancellationToken ct = default)
    {
        var report = await _reports.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Impact analysis report '{id}' was not found.");
        if (report.ProjectId != projectId)
            throw new NotFoundException($"Impact analysis report '{id}' was not found.");
        return report;
    }
}
