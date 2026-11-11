using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Coordination;

/// <summary>
/// A locked snapshot of approved revisions and dependencies that serves as the foundation 
/// for an execution campaign (SRS MCP-035).
/// </summary>
public sealed class PlanBaseline
{
    public BaselineId Id { get; }
    public TenantId TenantId { get; }
    public ProjectId ProjectId { get; }
    public string Goal { get; }
    public IReadOnlyList<Guid> ApprovedRevisionIds { get; }
    public IReadOnlyList<Guid> DependencyIds { get; }
    public IReadOnlyList<string> AcceptanceCriteria { get; }
    public int ExpectedPlanVersion { get; }
    public DateTimeOffset CreatedAt { get; }

    private PlanBaseline(
        BaselineId id,
        TenantId tenantId,
        ProjectId projectId,
        string goal,
        IReadOnlyList<Guid> approvedRevisionIds,
        IReadOnlyList<Guid> dependencyIds,
        IReadOnlyList<string> acceptanceCriteria,
        int expectedPlanVersion,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        Goal = goal;
        ApprovedRevisionIds = approvedRevisionIds;
        DependencyIds = dependencyIds;
        AcceptanceCriteria = acceptanceCriteria;
        ExpectedPlanVersion = expectedPlanVersion;
        CreatedAt = createdAt;
    }

    public static PlanBaseline Lock(
        TenantId tenantId,
        ProjectId projectId,
        string goal,
        IReadOnlyList<Guid> approvedRevisionIds,
        IReadOnlyList<Guid> dependencyIds,
        IReadOnlyList<string> acceptanceCriteria,
        int expectedPlanVersion)
    {
        return new PlanBaseline(
            BaselineId.New(),
            tenantId,
            projectId,
            goal,
            approvedRevisionIds,
            dependencyIds,
            acceptanceCriteria,
            expectedPlanVersion,
            DateTimeOffset.UtcNow);
    }
}
