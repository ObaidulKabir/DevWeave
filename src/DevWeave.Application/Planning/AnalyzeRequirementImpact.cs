using DevWeave.Application.Abstractions;
using DevWeave.Application.Lifecycle;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Planning;

public sealed class AnalyzeRequirementImpact
{
    private readonly IRequirementChangeSetRepository _changeSets;
    private readonly IImpactAnalysisReportRepository _reports;
    private readonly ImpactAnalysisService _analyzer;
    private readonly IProjectMembershipRepository _memberships;
    private readonly IUnitOfWork _unitOfWork;

    public AnalyzeRequirementImpact(
        IRequirementChangeSetRepository changeSets,
        IImpactAnalysisReportRepository reports,
        ImpactAnalysisService analyzer,
        IProjectMembershipRepository memberships,
        IUnitOfWork unitOfWork)
    {
        _changeSets = changeSets;
        _reports = reports;
        _analyzer = analyzer;
        _memberships = memberships;
        _unitOfWork = unitOfWork;
    }

    public async Task<ImpactAnalysisReport> ExecuteAsync(
        TenantId tenantId,
        ProjectId projectId,
        RequirementChangeSetId changeSetId,
        PrincipalId analyzedBy,
        CancellationToken ct = default)
    {
        var changeSet = await _changeSets.GetByIdAsync(tenantId, changeSetId, ct)
            ?? throw new NotFoundException($"Requirement change set '{changeSetId}' was not found.");
        if (changeSet.ProjectId != projectId)
            throw new NotFoundException($"Requirement change set '{changeSetId}' was not found.");

        await ProjectWorkAuthorization.EnsureMemberAsync(
            _memberships, tenantId, changeSet.ProjectId, analyzedBy, reviewerOnly: true, ct);

        var report = await _analyzer.AnalyzeAsync(changeSet, analyzedBy, ct);
        await _reports.AddAsync(report, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return report;
    }
}
