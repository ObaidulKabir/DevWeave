using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevWeave.Infrastructure.Repositories;

internal sealed class ProjectMembershipRepository : IProjectMembershipRepository
{
    private readonly DevWeaveDbContext _db;

    public ProjectMembershipRepository(DevWeaveDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(ProjectMembership membership, CancellationToken ct = default)
    {
        _db.ProjectMemberships.Add(membership);
        return Task.CompletedTask;
    }

    // Scoped read: the owning tenant is always part of the predicate.
    public Task<ProjectMembership?> GetByIdAsync(TenantId tenantId, ProjectMembershipId id, CancellationToken ct = default) =>
        _db.ProjectMemberships.SingleOrDefaultAsync(m => m.TenantId == tenantId && m.Id == id, ct);

    public async Task<IReadOnlyList<ProjectMembership>> ListByProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default) =>
        await _db.ProjectMemberships
            .Where(m => m.TenantId == tenantId && m.ProjectId == projectId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ProjectMembership>> ListByPrincipalAsync(TenantId tenantId, PrincipalId principalId, CancellationToken ct = default) =>
        await _db.ProjectMemberships
            .Where(m => m.TenantId == tenantId && m.PrincipalId == principalId)
            .ToListAsync(ct);
}