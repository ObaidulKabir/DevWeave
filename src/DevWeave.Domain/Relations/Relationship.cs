using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Relations;

/// <summary>
/// A typed, directed edge between two <see cref="KnowledgeItem"/>s (SRS REL-001). The edge is owned
/// by one tenant and one project and conforms (at creation, validated in the application layer) to an
/// Active <see cref="RelationType"/>. Revocation is terminal and hides the edge from traversal.
/// </summary>
public sealed class Relationship
{
    public RelationshipId Id { get; }
    public TenantId TenantId { get; }
    public ProjectId ProjectId { get; }
    public KnowledgeItemId SourceItemId { get; }
    public KnowledgeItemId TargetItemId { get; }
    public string RelationTypeCode { get; }
    public RelationshipStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public PrincipalId CreatedBy { get; }

    private Relationship(
        RelationshipId id,
        TenantId tenantId,
        ProjectId projectId,
        KnowledgeItemId sourceItemId,
        KnowledgeItemId targetItemId,
        string relationTypeCode,
        RelationshipStatus status,
        DateTimeOffset createdAt,
        PrincipalId createdBy)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        SourceItemId = sourceItemId;
        TargetItemId = targetItemId;
        RelationTypeCode = relationTypeCode;
        Status = status;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
    }

    public static Relationship Create(
        TenantId tenantId,
        ProjectId projectId,
        KnowledgeItemId sourceItemId,
        KnowledgeItemId targetItemId,
        string relationTypeCode,
        PrincipalId createdBy)
    {
        if (string.IsNullOrWhiteSpace(relationTypeCode))
            throw new ArgumentException("Relation type code is required.", nameof(relationTypeCode));

        return new Relationship(
            RelationshipId.New(),
            tenantId,
            projectId,
            sourceItemId,
            targetItemId,
            relationTypeCode,
            RelationshipStatus.Active,
            DateTimeOffset.UtcNow,
            createdBy);
    }

    public void Revoke()
    {
        if (Status == RelationshipStatus.Revoked)
            throw new InvalidOperationException("Relationship is already revoked.");

        Status = RelationshipStatus.Revoked;
    }
}