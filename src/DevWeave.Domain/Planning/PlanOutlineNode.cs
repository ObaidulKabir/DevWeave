using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Planning;

// A structural tree is separate from semantic knowledge relationships.
public sealed class PlanOutlineNode
{
    public KnowledgeItemId ItemId { get; private set; } = null!;
    public TenantId TenantId { get; private set; } = null!;
    public ProjectId ProjectId { get; private set; } = null!;
    public KnowledgeItemId RootItemId { get; private set; } = null!;
    public KnowledgeItemId? ParentItemId { get; private set; }
    public int Position { get; private set; }
    public PrincipalId CreatedBy { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    private PlanOutlineNode() { }

    public static PlanOutlineNode Create(
        TenantId tenantId, ProjectId projectId, KnowledgeItemId itemId,
        KnowledgeItemId rootItemId, KnowledgeItemId? parentItemId, int position, PrincipalId actor)
    {
        if (position < 0 || parentItemId == itemId)
            throw new ArgumentException("Outline position must be nonnegative and an item cannot parent itself.");
        if (parentItemId is null && rootItemId != itemId)
            throw new ArgumentException("A root must identify itself.");
        if (parentItemId is not null && rootItemId == itemId)
            throw new ArgumentException("A child cannot identify itself as the root.");
        return new PlanOutlineNode
        {
            TenantId = tenantId, ProjectId = projectId, ItemId = itemId,
            RootItemId = rootItemId, ParentItemId = parentItemId,
            Position = position, CreatedBy = actor, CreatedAt = DateTimeOffset.UtcNow,
        };
    }
}
