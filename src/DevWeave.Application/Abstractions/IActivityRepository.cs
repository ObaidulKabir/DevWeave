using DevWeave.Domain.Activities;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Abstractions;

/// <summary>
/// Provenance store port for the append-only <see cref="Activity"/> aggregate (SRS PRO-001, REV-006).
/// Deliberately exposes only <c>Add</c> plus tenant-scoped reads: activities are immutable audit
/// facts, so there is no Update or Remove path by design.
/// </summary>
public interface IActivityRepository
{
    Task AddAsync(Activity activity, CancellationToken ct = default);

    Task<Activity?> GetByIdAsync(TenantId tenantId, ActivityId id, CancellationToken ct = default);

    Task<IReadOnlyList<Activity>> ListForProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default);

    Task<IReadOnlyList<Activity>> ListForItemAsync(TenantId tenantId, KnowledgeItemId itemId, CancellationToken ct = default);

    Task<IReadOnlyList<Activity>> ListByActorAsync(TenantId tenantId, PrincipalId actorId, CancellationToken ct = default);

    Task<Activity?> FindByCorrelationIdAsync(
        TenantId tenantId,
        ProjectId projectId,
        PrincipalId actorId,
        string correlationId,
        CancellationToken ct = default);
}