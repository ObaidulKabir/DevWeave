using DevWeave.Application.Abstractions;
using DevWeave.Domain.Activities;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;

namespace DevWeave.Application.Activities;

/// <summary>Lists every activity whose inputs or outputs reference a given knowledge item.</summary>
public sealed class ListActivitiesForItem
{
    private readonly IActivityRepository _activities;

    public ListActivitiesForItem(IActivityRepository activities)
    {
        _activities = activities;
    }

    public Task<IReadOnlyList<Activity>> ExecuteAsync(TenantId tenantId, KnowledgeItemId itemId, CancellationToken ct = default) =>
        _activities.ListForItemAsync(tenantId, itemId, ct);
}