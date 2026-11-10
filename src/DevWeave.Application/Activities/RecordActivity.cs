using DevWeave.Application.Abstractions;
using DevWeave.Domain.Activities;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Activities;

/// <summary>
/// Records an append-only provenance activity (SRS PRO-001, PRO-002). It enforces deny-by-default:
/// the actor and the routed project must both resolve inside the tenant before an activity is committed.
/// The domain aggregate guards the REV-008 amendment rule (reason + referenced revision), which is
/// surfaced here as a <see cref="ConflictException"/> so the API can map it to HTTP 409.
/// </summary>
public sealed class RecordActivity
{
    private readonly IActivityRepository _activities;
    private readonly IPrincipalRepository _principals;
    private readonly IProjectRepository _projects;
    private readonly IUnitOfWork _unitOfWork;

    public RecordActivity(
        IActivityRepository activities,
        IPrincipalRepository principals,
        IProjectRepository projects,
        IUnitOfWork unitOfWork)
    {
        _activities = activities;
        _principals = principals;
        _projects = projects;
        _unitOfWork = unitOfWork;
    }

    public async Task<Activity> ExecuteAsync(
        TenantId tenantId,
        ProjectId projectId,
        ActivityType type,
        PrincipalId actor,
        string purpose,
        ActivityStatus status,
        DateTimeOffset startedAt,
        DateTimeOffset? endedAt,
        string? correlationId,
        string? reason,
        KnowledgeRevisionId? reference,
        IReadOnlyList<ActivityInput> inputs,
        IReadOnlyList<ActivityOutput> outputs,
        CancellationToken ct = default)
    {
        // Deny-by-default: a cross-tenant actor or project is indistinguishable from a missing one.
        _ = await _principals.GetByIdAsync(tenantId, actor, ct)
            ?? throw new NotFoundException($"Principal '{actor}' was not found within tenant '{tenantId}'.");
        _ = await _projects.GetByIdAsync(tenantId, projectId, ct)
            ?? throw new NotFoundException($"Project '{projectId}' was not found within tenant '{tenantId}'.");

        try
        {
            var activity = Activity.Create(
                tenantId,
                projectId,
                type,
                actor,
                purpose,
                status,
                startedAt,
                endedAt,
                correlationId,
                reason,
                reference,
                inputs,
                outputs);

            await _activities.AddAsync(activity, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return activity;
        }
        catch (ArgumentException ex) when (type is ActivityType.Correction or ActivityType.Retraction or ActivityType.Supersession)
        {
            // REV-008: an amendment submitted without its reason/reference cannot be committed.
            throw new ConflictException(ex.Message);
        }
    }
}