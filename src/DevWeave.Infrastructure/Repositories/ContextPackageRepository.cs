using DevWeave.Application.Abstractions;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevWeave.Infrastructure.Repositories;

internal sealed class ContextPackageRepository : IContextPackageRepository
{
    private readonly DevWeaveDbContext _db;

    public ContextPackageRepository(DevWeaveDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(ContextPackage package, CancellationToken ct = default)
    {
        _db.ContextPackages.Add(package);
        return Task.CompletedTask;
    }

    // Deny-by-default: a cross-tenant package is indistinguishable from a missing one.
    public Task<ContextPackage?> GetByIdAsync(TenantId tenantId, ContextPackageId id, CancellationToken ct = default) =>
        _db.ContextPackages.SingleOrDefaultAsync(p => p.TenantId == tenantId && p.Id == id, ct);

    // AGT-010: a retry with the same idempotency key, scoped to the same agent, resolves to the
    // already-assembled package rather than a duplicate. Stale packages never satisfy retries.
    public Task<ContextPackage?> FindByIdempotencyKeyAsync(
        TenantId tenantId,
        ProjectId projectId,
        PrincipalId agentId,
        string idempotencyKey,
        CancellationToken ct = default) =>
        _db.ContextPackages.SingleOrDefaultAsync(
            p => p.TenantId == tenantId
                && p.ProjectId == projectId
                && p.AgentId == agentId
                && p.IdempotencyKey == idempotencyKey
                && !p.IsStale,
            ct);

    public async Task<IReadOnlyList<ContextPackage>> ListCurrentByProjectAsync(
        TenantId tenantId,
        ProjectId projectId,
        CancellationToken ct = default) =>
        await _db.ContextPackages
            .Where(package => package.TenantId == tenantId
                && package.ProjectId == projectId
                && !package.IsStale)
            .OrderByDescending(package => package.CreatedAt)
            .ToListAsync(ct);

    public Task<ContextPackage?> GetLatestCurrentForTaskAsync(
        TenantId tenantId,
        ProjectId projectId,
        string taskKey,
        CancellationToken ct = default) =>
        _db.ContextPackages
            .Where(package => package.TenantId == tenantId
                && package.ProjectId == projectId
                && package.TaskKey == taskKey
                && !package.IsStale)
            .OrderByDescending(package => package.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public Task<ContextPackage?> GetLatestForTaskAsync(
        TenantId tenantId,
        ProjectId projectId,
        string taskKey,
        CancellationToken ct = default) =>
        _db.ContextPackages
            .Where(package => package.TenantId == tenantId
                && package.ProjectId == projectId
                && package.TaskKey == taskKey)
            .OrderByDescending(package => package.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<ContextPackage>> ListCurrentPinningRevisionsAsync(
        TenantId tenantId,
        ProjectId projectId,
        IReadOnlyCollection<KnowledgeRevisionId> revisionIds,
        CancellationToken ct = default)
    {
        if (revisionIds.Count == 0)
            return Array.Empty<ContextPackage>();

        var revisionValues = revisionIds.Select(id => id.Value).ToHashSet();
        var packages = await _db.ContextPackages
            .Where(package => package.TenantId == tenantId
                && package.ProjectId == projectId
                && !package.IsStale)
            .ToListAsync(ct);

        return packages
            .Where(package => package.Entries.Any(entry => revisionValues.Contains(entry.RevisionId.Value)))
            .ToList();
    }
}
