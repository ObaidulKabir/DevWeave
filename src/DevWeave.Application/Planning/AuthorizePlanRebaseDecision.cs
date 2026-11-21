using DevWeave.Application.Abstractions;
using DevWeave.Application.Lifecycle;
using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Planning;

/// <summary>
/// Records an Owner/Admin rebase decision for one work package against an impact report, then resumes
/// the package's campaign when the decision allows continuation.
/// </summary>
public sealed class AuthorizePlanRebaseDecision
{
    private readonly IImpactAnalysisReportRepository _reports;
    private readonly IPlanRebaseDecisionRepository _decisions;
    private readonly ICoordinationRepository _coordination;
    private readonly IProjectMembershipRepository _memberships;
    private readonly IUnitOfWork _unitOfWork;

    public AuthorizePlanRebaseDecision(
        IImpactAnalysisReportRepository reports,
        IPlanRebaseDecisionRepository decisions,
        ICoordinationRepository coordination,
        IProjectMembershipRepository memberships,
        IUnitOfWork unitOfWork)
    {
        _reports = reports;
        _decisions = decisions;
        _coordination = coordination;
        _memberships = memberships;
        _unitOfWork = unitOfWork;
    }

    public async Task<PlanRebaseDecision> ExecuteAsync(
        TenantId tenantId,
        ProjectId projectId,
        ImpactAnalysisReportId impactReportId,
        WorkPackageId workPackageId,
        PlanRebaseDecisionKind kind,
        PrincipalId authorizedBy,
        string reason,
        CancellationToken ct = default)
    {
        var report = await _reports.GetByIdAsync(tenantId, impactReportId, ct)
            ?? throw new NotFoundException($"Impact analysis report '{impactReportId}' was not found.");
        if (report.ProjectId != projectId)
            throw new NotFoundException($"Impact analysis report '{impactReportId}' was not found.");

        await ProjectWorkAuthorization.EnsureMemberAsync(
            _memberships, tenantId, report.ProjectId, authorizedBy, reviewerOnly: true, ct);

        var package = await _coordination.GetWorkPackageByIdAsync(tenantId, workPackageId, ct)
            ?? throw new NotFoundException($"Work package '{workPackageId}' was not found.");
        if (package.ProjectId != report.ProjectId)
            throw new NotFoundException($"Work package '{workPackageId}' was not found within this project.");

        var decision = PlanRebaseDecision.Authorize(
            tenantId,
            report.ProjectId,
            workPackageId,
            impactReportId,
            kind,
            authorizedBy,
            reason);

        await _decisions.AddAsync(decision, ct);

        if (decision.AllowsCampaignResume)
        {
            var campaign = await _coordination.GetCampaignByIdAsync(tenantId, package.CampaignId, ct)
                ?? throw new NotFoundException($"Campaign '{package.CampaignId}' was not found.");
            if (campaign.Status == CampaignStatus.Suspended)
                campaign.Resume();
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return decision;
    }
}
