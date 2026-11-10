using DevWeave.Application.Abstractions;
using DevWeave.Domain.Activities;
using DevWeave.Domain.Identity;

namespace DevWeave.Application.Activities;

/// <summary>Lists every activity recorded by a given actor (PRO-002 actor attribution).</summary>
public sealed class ListActivitiesByActor
{
    private readonly IActivityRepository _activities;

    public ListActivitiesByActor(IActivityRepository activities)
    {
        _activities = activities;
    }

    public Task<IReadOnlyList<Activity>> ExecuteAsync(TenantId tenantId, PrincipalId actorId, CancellationToken ct = default) =>
        _activities.ListByActorAsync(tenantId, actorId, ct);
}