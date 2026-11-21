using System.Text.Json;
using DevWeave.Application.Abstractions;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Application.Lifecycle;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Planning;

public sealed class AnalyzeRequirementImpact
{
    private readonly IRequirementChangeSetRepository _changeSets;
    private readonly IImpactAnalysisReportRepository _reports;
    private readonly ImpactAnalysisService _analyzer;
    private readonly IContextPackageRepository _contexts;
    private readonly ITransactionalOutbox _outbox;
    private readonly IProjectMembershipRepository _memberships;
    private readonly IUnitOfWork _unitOfWork;

    public AnalyzeRequirementImpact(
        IRequirementChangeSetRepository changeSets,
        IImpactAnalysisReportRepository reports,
        ImpactAnalysisService analyzer,
        IContextPackageRepository contexts,
        ITransactionalOutbox outbox,
        IProjectMembershipRepository memberships,
        IUnitOfWork unitOfWork)
    {
        _changeSets = changeSets;
        _reports = reports;
        _analyzer = analyzer;
        _contexts = contexts;
        _outbox = outbox;
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

        var revisionIds = changeSet.Changes
            .SelectMany(entry => entry.PreviousRevisionIds.Concat(entry.ResultRevisionIds))
            .Distinct()
            .Select(id => new KnowledgeRevisionId(id))
            .ToList();

        var stalePackages = await _contexts.ListCurrentPinningRevisionsAsync(
            tenantId, projectId, revisionIds, ct);
        foreach (var package in stalePackages)
        {
            package.MarkStale(
                $"Impact report '{report.Id.Value}' invalidated pinned revision inputs.");
        }

        await _outbox.EnqueueAsync(
            new AuthorizedScope(
                tenantId,
                analyzedBy,
                projectId,
                "requirement.impact.analyzed",
                report.Id.Value.ToString("N")),
            "requirement.impact.analyzed",
            "1.0",
            JsonSerializer.Serialize(new
            {
                reportId = report.Id.Value,
                changeSetId = changeSet.Id.Value,
                projectId = projectId.Value,
                staleContextCount = stalePackages.Count,
                findingCount = report.Findings.Count,
            }),
            ct);

        await _unitOfWork.SaveChangesAsync(ct);
        return report;
    }
}
