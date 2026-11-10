using DevWeave.Application.Abstractions;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;
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
    // already-assembled package.
    public Task<ContextPackage?> FindByIdempotencyKeyAsync(
        TenantId tenantId,
        ProjectId projectId,
        PrincipalId agentId,
        string idempotencyKey,
        CancellationToken ct = default) =>
        _db.ContextPackages.SingleOrDefaultAsync(
            p => p.TenantId == tenantId && p.ProjectId == projectId && p.AgentId == agentId && p.IdempotencyKey == idempotencyKey,
            ct);
}