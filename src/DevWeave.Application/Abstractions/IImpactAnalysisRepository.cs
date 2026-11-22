using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Abstractions;

public interface IRequirementChangeSetRepository
{
    Task AddAsync(RequirementChangeSet changeSet, CancellationToken ct = default);

    Task<RequirementChangeSet?> GetByIdAsync(
        TenantId tenantId,
        RequirementChangeSetId id,
        CancellationToken ct = default);
}

public interface IImpactAnalysisReportRepository
{
    Task AddAsync(ImpactAnalysisReport report, CancellationToken ct = default);

    Task<ImpactAnalysisReport?> GetByIdAsync(
        TenantId tenantId,
        ImpactAnalysisReportId id,
        CancellationToken ct = default);

    Task<IReadOnlyList<ImpactAnalysisReport>> ListByProjectAsync(
        TenantId tenantId,
        ProjectId projectId,
        CancellationToken ct = default);
}

public interface IPlanRebaseDecisionRepository
{
    Task AddAsync(PlanRebaseDecision decision, CancellationToken ct = default);

    Task<PlanRebaseDecision?> GetByIdAsync(
        TenantId tenantId,
        PlanRebaseDecisionId id,
        CancellationToken ct = default);

    Task<PlanRebaseDecision?> GetLatestForWorkPackageAsync(
        TenantId tenantId,
        WorkPackageId workPackageId,
        CancellationToken ct = default);

    Task<IReadOnlyList<PlanRebaseDecision>> ListByImpactReportAsync(
        TenantId tenantId,
        ImpactAnalysisReportId impactReportId,
        CancellationToken ct = default);
}
