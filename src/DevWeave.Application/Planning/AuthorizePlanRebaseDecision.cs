using System.Text.Json;
using DevWeave.Application.Abstractions;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Application.Lifecycle;
using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Planning;

/// <summary>
/// Records an Owner/Admin rebase decision for one work package against an impact report, creates a
/// continuation package when continuation is authorized, then resumes the campaign when allowed.
/// </summary>
public sealed class AuthorizePlanRebaseDecision
{
    private readonly IImpactAnalysisReportRepository _reports;
    private readonly IPlanRebaseDecisionRepository _decisions;
    private readonly ICoordinationRepository _coordination;
    private readonly ITransactionalOutbox _outbox;
    private readonly IProjectMembershipRepository _memberships;
    private readonly IUnitOfWork _unitOfWork;

    public AuthorizePlanRebaseDecision(
        IImpactAnalysisReportRepository reports,
        IPlanRebaseDecisionRepository decisions,
        ICoordinationRepository coordination,
        ITransactionalOutbox outbox,
        IProjectMembershipRepository memberships,
        IUnitOfWork unitOfWork)
    {
        _reports = reports;
        _decisions = decisions;
        _coordination = coordination;
        _outbox = outbox;
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

        if (package.IsUnscoped && kind is PlanRebaseDecisionKind.ContinueUnchanged
                or PlanRebaseDecisionKind.ContinueAfterContextRefresh)
        {
            throw new ConflictException(
                "Unscoped work packages cannot continue without an explicit replan or supersede decision.");
        }

        var decision = PlanRebaseDecision.Authorize(
            tenantId,
            report.ProjectId,
            workPackageId,
            impactReportId,
            kind,
            authorizedBy,
            reason);

        if (decision.CreatesContinuation)
        {
            var baselines = await _coordination.ListBaselinesByProjectAsync(tenantId, projectId, ct);
            var latestBaseline = baselines.OrderByDescending(baseline => baseline.CreatedAt).FirstOrDefault()
                ?? throw new ConflictException("A continuation package requires a locked plan baseline.");

            var continuationKey = $"{package.Key}.cont.{decision.Id.Value:N}"[..Math.Min(
                100,
                $"{package.Key}.cont.{decision.Id.Value:N}".Length)];
            var continuation = WorkPackage.CreateContinuation(package, latestBaseline.Id.Value, continuationKey);
            await _coordination.AddWorkPackageAsync(continuation, ct);
            decision.AttachContinuation(continuation.Id);

            if (package.IsOpen)
                package.Cancel();
        }

        await _decisions.AddAsync(decision, ct);

        if (decision.AllowsCampaignResume)
        {
            var campaign = await _coordination.GetCampaignByIdAsync(tenantId, package.CampaignId, ct)
                ?? throw new NotFoundException($"Campaign '{package.CampaignId}' was not found.");
            if (campaign.Status == CampaignStatus.Suspended)
                campaign.Resume();
        }

        await _outbox.EnqueueAsync(
            new AuthorizedScope(
                tenantId,
                authorizedBy,
                projectId,
                "plan.rebase.authorized",
                decision.Id.Value.ToString("N")),
            "plan.rebase.authorized",
            "1.0",
            JsonSerializer.Serialize(new
            {
                decisionId = decision.Id.Value,
                impactReportId = report.Id.Value,
                workPackageId = package.Id.Value,
                continuationWorkPackageId = decision.ContinuationWorkPackageId?.Value,
                kind = decision.Kind.ToString(),
                requiresContextRefresh = decision.RequiresContextRefresh,
            }),
            ct);

        await _unitOfWork.SaveChangesAsync(ct);
        return decision;
    }
}
