using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Lifecycle;

public sealed class MilestoneTransition
{
    public MilestoneTransitionId Id { get; }
    public TenantId TenantId { get; }
    public ProjectId ProjectId { get; }
    public MilestoneId MilestoneId { get; }
    public MilestoneState FromState { get; }
    public MilestoneState ToState { get; }
    public PrincipalId ActorId { get; }
    public string Reason { get; }
    public int Version { get; }
    public DateTimeOffset RecordedAt { get; }

    private MilestoneTransition()
    {
        Id = null!;
        TenantId = null!;
        ProjectId = null!;
        MilestoneId = null!;
        ActorId = null!;
        Reason = null!;
    }

    private MilestoneTransition(Milestone milestone, MilestoneState from, MilestoneState to,
        PrincipalId actorId, string reason)
    {
        Id = MilestoneTransitionId.New();
        TenantId = milestone.TenantId;
        ProjectId = milestone.ProjectId;
        MilestoneId = milestone.Id;
        FromState = from;
        ToState = to;
        ActorId = actorId;
        Reason = reason;
        Version = milestone.Version;
        RecordedAt = DateTimeOffset.UtcNow;
    }

    internal static MilestoneTransition Create(Milestone milestone, MilestoneState from,
        MilestoneState to, PrincipalId actorId, string reason) =>
        new(milestone, from, to, actorId, reason);
}
