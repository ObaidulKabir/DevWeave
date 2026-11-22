using DevWeave.Application.Abstractions;
using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevWeave.Infrastructure.Repositories;

internal sealed class PlanBaselineManifestRepository(DevWeaveDbContext db)
    : IPlanBaselineManifestRepository
{
    public Task AddAsync(PlanBaselineV2 manifest, CancellationToken ct = default)
    {
        db.PlanBaselineManifests.Add(manifest);
        return Task.CompletedTask;
    }

    public Task<PlanBaselineV2?> GetByIdAsync(
        TenantId tenantId,
        ProjectId projectId,
        BaselineId baselineId,
        CancellationToken ct = default) =>
        db.PlanBaselineManifests
            .Include(manifest => manifest.ApprovedRevisions)
            .Include(manifest => manifest.SemanticEdges)
            .Include(manifest => manifest.SpecificationBindings)
            .Include(manifest => manifest.CriterionRefs)
            .Include(manifest => manifest.DependencyGraph)
            .SingleOrDefaultAsync(manifest =>
                manifest.TenantId == tenantId &&
                manifest.ProjectId == projectId &&
                manifest.Id == baselineId,
                ct);
}
