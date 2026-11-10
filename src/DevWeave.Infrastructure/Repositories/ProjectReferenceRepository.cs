using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevWeave.Infrastructure.Repositories;

internal sealed class ProjectReferenceRepository : IProjectReferenceRepository
{
    private readonly DevWeaveDbContext _db;

    public ProjectReferenceRepository(DevWeaveDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(ProjectReference reference, CancellationToken ct = default)
    {
        _db.ProjectReferences.Add(reference);
        return Task.CompletedTask;
    }

    // Scoped read: the owning tenant is always part of the predicate.
    public async Task<IReadOnlyList<ProjectReference>> ListByProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default) =>
        await _db.ProjectReferences
            .Where(r => r.TenantId == tenantId && r.ProjectId == projectId)
            .ToListAsync(ct);
}