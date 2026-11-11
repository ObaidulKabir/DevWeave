using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Planning;

public sealed record PlanWorkLinkId(Guid Value)
{
    public static PlanWorkLinkId New() => new(Guid.NewGuid());
}

// A link pins the reviewed basis of work to a specific immutable knowledge revision.
public sealed class PlanWorkLink
{
    public PlanWorkLinkId Id { get; private set; } = null!;
    public TenantId TenantId { get; private set; } = null!;
    public ProjectId ProjectId { get; private set; } = null!;
    public WorkItemId WorkItemId { get; private set; } = null!;
    public KnowledgeItemId KnowledgeItemId { get; private set; } = null!;
    public KnowledgeRevisionId RevisionId { get; private set; } = null!;
    public PrincipalId CreatedBy { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    private PlanWorkLink() { }

    public static PlanWorkLink Create(TenantId tenantId, ProjectId projectId, WorkItemId workItemId,
        KnowledgeItemId knowledgeItemId, KnowledgeRevisionId revisionId, PrincipalId actor) =>
        new()
        {
            Id = PlanWorkLinkId.New(), TenantId = tenantId, ProjectId = projectId,
            WorkItemId = workItemId, KnowledgeItemId = knowledgeItemId, RevisionId = revisionId,
            CreatedBy = actor, CreatedAt = DateTimeOffset.UtcNow,
        };
}
