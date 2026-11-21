using DevWeave.Application.Abstractions;
using DevWeave.Application.Lifecycle;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Planning;

public sealed class ListImpactAnalysisReportsForProject
{
    private readonly IImpactAnalysisReportRepository _reports;
    private readonly IProjectMembershipRepository _memberships;

    public ListImpactAnalysisReportsForProject(
        IImpactAnalysisReportRepository reports,
        IProjectMembershipRepository memberships)
    {
        _reports = reports;
        _memberships = memberships;
    }

    public async Task<IReadOnlyList<ImpactAnalysisReport>> ExecuteAsync(
        TenantId tenantId,
        ProjectId projectId,
        PrincipalId actorId,
        CancellationToken ct = default)
    {
        await ProjectWorkAuthorization.EnsureMemberAsync(
            _memberships, tenantId, projectId, actorId, reviewerOnly: false, ct);

        return await _reports.ListByProjectAsync(tenantId, projectId, ct);
    }
}
