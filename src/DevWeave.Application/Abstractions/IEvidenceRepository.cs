using DevWeave.Domain.Activities;
using DevWeave.Domain.Artifacts;
using DevWeave.Domain.Evidence;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using EvidenceRecord = DevWeave.Domain.Evidence.Evidence;

namespace DevWeave.Application.Abstractions;

/// <summary>
/// Tenant-scoped evidence store port (SRS PRO-006). Evidence is append-only for its immutable fields;
/// the only mutation is the review-state transition, which is expressed as a read-modify-write through
/// the repository and guarded by the domain aggregate.
/// </summary>
public interface IEvidenceRepository
{
    Task AddAsync(EvidenceRecord evidence, CancellationToken ct = default);

    Task<EvidenceRecord?> GetByIdAsync(TenantId tenantId, EvidenceId id, CancellationToken ct = default);

    Task<IReadOnlyList<EvidenceRecord>> ListForClaimAsync(TenantId tenantId, ActivityId originActivityId, CancellationToken ct = default);

    Task<IReadOnlyList<EvidenceRecord>> ListForProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default);
}