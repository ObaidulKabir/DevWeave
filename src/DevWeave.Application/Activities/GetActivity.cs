using DevWeave.Application.Abstractions;
using DevWeave.Domain.Activities;
using DevWeave.Domain.Identity;

namespace DevWeave.Application.Activities;

/// <summary>Reads a single appended activity by id, tenant-scoped (deny-by-default).</summary>
public sealed class GetActivity
{
    private readonly IActivityRepository _activities;

    public GetActivity(IActivityRepository activities)
    {
        _activities = activities;
    }

    public Task<Activity?> ExecuteAsync(TenantId tenantId, ActivityId id, CancellationToken ct = default) =>
        _activities.GetByIdAsync(tenantId, id, ct);
}