using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Abstractions;

/// <summary>
/// Storage port for <see cref="ContextPackage"/> aggregates (SRS AGT-004, AGT-010). Reads are
/// tenant-scoped so a cross-tenant package is indistinguishable from a missing one (deny-by-default).
/// </summary>
public interface IContextPackageRepository
{
    Task AddAsync(ContextPackage package, CancellationToken ct = default);

    Task<ContextPackage?> GetByIdAsync(TenantId tenantId, ContextPackageId id, CancellationToken ct = default);

    Task<ContextPackage?> FindByIdempotencyKeyAsync(
        TenantId tenantId,
        ProjectId projectId,
        PrincipalId agentId,
        string idempotencyKey,
        CancellationToken ct = default);
}