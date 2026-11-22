using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Abstractions;

public interface IPlanBaselineManifestRepository
{
    Task AddAsync(PlanBaselineV2 manifest, CancellationToken ct = default);

    Task<PlanBaselineV2?> GetByIdAsync(
        TenantId tenantId,
        ProjectId projectId,
        BaselineId baselineId,
        CancellationToken ct = default);
}
