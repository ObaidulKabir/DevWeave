using DevWeave.Application.Abstractions;
using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevWeave.Infrastructure.Repositories;

internal sealed class RequirementChangeSetRepository(DevWeaveDbContext db) : IRequirementChangeSetRepository
{
    public Task AddAsync(RequirementChangeSet changeSet, CancellationToken ct = default)
    {
        db.RequirementChangeSets.Add(changeSet);
        return Task.CompletedTask;
    }

    public Task<RequirementChangeSet?> GetByIdAsync(
        TenantId tenantId,
        RequirementChangeSetId id,
        CancellationToken ct = default) =>
        db.RequirementChangeSets
            .SingleOrDefaultAsync(set => set.TenantId == tenantId && set.Id == id, ct);
}

internal sealed class ImpactAnalysisReportRepository(DevWeaveDbContext db) : IImpactAnalysisReportRepository
{
    public Task AddAsync(ImpactAnalysisReport report, CancellationToken ct = default)
    {
        db.ImpactAnalysisReports.Add(report);
        return Task.CompletedTask;
    }

    public Task<ImpactAnalysisReport?> GetByIdAsync(
        TenantId tenantId,
        ImpactAnalysisReportId id,
        CancellationToken ct = default) =>
        db.ImpactAnalysisReports
            .SingleOrDefaultAsync(report => report.TenantId == tenantId && report.Id == id, ct);

    public async Task<IReadOnlyList<ImpactAnalysisReport>> ListByProjectAsync(
        TenantId tenantId,
        ProjectId projectId,
        CancellationToken ct = default) =>
        await db.ImpactAnalysisReports
            .Where(report => report.TenantId == tenantId && report.ProjectId == projectId)
            .OrderByDescending(report => report.AnalyzedAt)
            .ToListAsync(ct);
}

internal sealed class PlanRebaseDecisionRepository(DevWeaveDbContext db) : IPlanRebaseDecisionRepository
{
    public Task AddAsync(PlanRebaseDecision decision, CancellationToken ct = default)
    {
        db.PlanRebaseDecisions.Add(decision);
        return Task.CompletedTask;
    }

    public Task<PlanRebaseDecision?> GetByIdAsync(
        TenantId tenantId,
        PlanRebaseDecisionId id,
        CancellationToken ct = default) =>
        db.PlanRebaseDecisions
            .SingleOrDefaultAsync(decision => decision.TenantId == tenantId && decision.Id == id, ct);

    public Task<PlanRebaseDecision?> GetLatestForWorkPackageAsync(
        TenantId tenantId,
        WorkPackageId workPackageId,
        CancellationToken ct = default) =>
        db.PlanRebaseDecisions
            .Where(decision => decision.TenantId == tenantId && decision.WorkPackageId == workPackageId)
            .OrderByDescending(decision => decision.AuthorizedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<PlanRebaseDecision>> ListByImpactReportAsync(
        TenantId tenantId,
        ImpactAnalysisReportId impactReportId,
        CancellationToken ct = default) =>
        await db.PlanRebaseDecisions
            .Where(decision => decision.TenantId == tenantId && decision.ImpactReportId == impactReportId)
            .OrderByDescending(decision => decision.AuthorizedAt)
            .ToListAsync(ct);
}
