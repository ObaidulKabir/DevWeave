using DevWeave.Application.Abstractions;
using DevWeave.Domain.Activities;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Activities;

/// <summary>Lists the ordered provenance timeline for one project (SRS PRO-001).</summary>
public sealed class ListActivitiesForProject
{
    private readonly IActivityRepository _activities;

    public ListActivitiesForProject(IActivityRepository activities)
    {
        _activities = activities;
    }

    public Task<IReadOnlyList<Activity>> ExecuteAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default) =>
        _activities.ListForProjectAsync(tenantId, projectId, ct);
}