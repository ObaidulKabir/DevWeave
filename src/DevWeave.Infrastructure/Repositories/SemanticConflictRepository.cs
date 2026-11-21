using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevWeave.Infrastructure.Repositories;

internal sealed class SemanticConflictRepository(DevWeaveDbContext db) : ISemanticConflictRepository
{
    public Task AddAsync(SemanticConflict conflict, CancellationToken ct = default)
    {
        db.SemanticConflicts.Add(conflict);
        return Task.CompletedTask;
    }

    public Task<SemanticConflict?> GetByIdAsync(
        TenantId tenantId,
        SemanticConflictId id,
        CancellationToken ct = default) =>
        db.SemanticConflicts.SingleOrDefaultAsync(
            conflict => conflict.TenantId == tenantId && conflict.Id == id, ct);

    public async Task<IReadOnlyList<SemanticConflict>> ListUnresolvedByProjectAsync(
        TenantId tenantId,
        ProjectId projectId,
        CancellationToken ct = default) =>
        await db.SemanticConflicts
            .Where(conflict =>
                conflict.TenantId == tenantId
                && conflict.ProjectId == projectId
                && !conflict.IsResolved)
            .OrderBy(conflict => conflict.RecordedAt)
            .ToListAsync(ct);
}
