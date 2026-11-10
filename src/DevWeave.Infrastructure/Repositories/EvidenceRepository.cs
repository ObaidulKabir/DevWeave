using DevWeave.Application.Abstractions;
using DevWeave.Domain.Activities;
using DevWeave.Domain.Evidence;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevWeave.Infrastructure.Repositories;

internal sealed class EvidenceRepository : IEvidenceRepository
{
    private readonly DevWeaveDbContext _db;

    public EvidenceRepository(DevWeaveDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(Evidence evidence, CancellationToken ct = default)
    {
        _db.Evidence.Add(evidence);
        return Task.CompletedTask;
    }

    // Deny-by-default: a cross-tenant evidence record is indistinguishable from a missing one.
    public Task<Evidence?> GetByIdAsync(TenantId tenantId, EvidenceId id, CancellationToken ct = default) =>
        _db.Evidence.SingleOrDefaultAsync(e => e.TenantId == tenantId && e.Id == id, ct);

    public async Task<IReadOnlyList<Evidence>> ListForClaimAsync(TenantId tenantId, ActivityId originActivityId, CancellationToken ct = default) =>
        await _db.Evidence
            .Where(e => e.TenantId == tenantId && e.OriginActivityId == originActivityId)
            .OrderBy(e => e.RecordedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Evidence>> ListForProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default) =>
        await _db.Evidence
            .Where(e => e.TenantId == tenantId && e.ProjectId == projectId)
            .OrderBy(e => e.RecordedAt)
            .ToListAsync(ct);
}