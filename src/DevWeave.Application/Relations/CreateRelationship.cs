using DevWeave.Application.Abstractions;
using DevWeave.Application.Lifecycle;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Relations;

namespace DevWeave.Application.Relations;

/// <summary>
/// Creates a typed, directed relationship between two knowledge items (SRS REL-001). It enforces the
/// registry rules in order: the relation type must be registered and Active (BR-007); both endpoints
/// must be resolvable inside the tenant (deny-by-default for REL-007); the endpoints must share the
/// routed project (REL-007); the endpoint knowledge types must be permitted (REL-002/SCH-007); and
/// the cardinality must not be exceeded (SCH-007).
/// </summary>
public sealed class CreateRelationship
{
    private readonly IRelationshipRepository _relationships;
    private readonly IRelationTypeRepository _relationTypes;
    private readonly IKnowledgeItemRepository _items;
    private readonly IProjectMembershipRepository _memberships;
    private readonly IUnitOfWork _unitOfWork;

    public CreateRelationship(
        IRelationshipRepository relationships,
        IRelationTypeRepository relationTypes,
        IKnowledgeItemRepository items,
        IProjectMembershipRepository memberships,
        IUnitOfWork unitOfWork)
    {
        _memberships = memberships;
        _relationships = relationships;
        _relationTypes = relationTypes;
        _items = items;
        _unitOfWork = unitOfWork;
    }

    public async Task<Relationship> ExecuteAsync(
        TenantId tenantId,
        ProjectId projectId,
        KnowledgeItemId sourceItemId,
        KnowledgeItemId targetItemId,
        string relationTypeCode,
        PrincipalId createdBy,
        CancellationToken ct = default)
    {
        // Only active project members who can edit (not Viewers) may change the plan's structure.
        await ProjectWorkAuthorization.EnsureMemberAsync(_memberships, tenantId, projectId, createdBy, reviewerOnly: false, ct);

        // BR-007: an unknown relation type is rejected outright.
        var relationType = await _relationTypes.GetByCodeAsync(relationTypeCode, ct)
            ?? throw new RelationTypeMismatchException(
                "unknown-relation-type",
                $"Relation type '{relationTypeCode}' is not registered.");

        if (!relationType.IsActive)
            throw new RelationTypeMismatchException(
                "relation-type-not-active",
                $"Relation type '{relationTypeCode}' is not active.");

        // Deny-by-default: a source/target from another tenant is indistinguishable from a missing item.
        var source = await _items.GetByIdAsync(tenantId, sourceItemId, ct)
            ?? throw new NotFoundException($"Knowledge item '{sourceItemId}' was not found within tenant '{tenantId}'.");
        var target = await _items.GetByIdAsync(tenantId, targetItemId, ct)
            ?? throw new NotFoundException($"Knowledge item '{targetItemId}' was not found within tenant '{tenantId}'.");

        // REL-007: no cross-project edges (same tenant, different ProjectId is rejected by default).
        if (source.ProjectId != projectId || target.ProjectId != projectId)
            throw new CrossProjectReferenceException(
                "Relationships are confined to a single project; source and target must both belong to the routed project.");

        // REL-002/SCH-007: validate the endpoint knowledge types against the registry rules.
        if (!relationType.Accepts(source.TypeId, target.TypeId))
            throw new RelationTypeMismatchException(
                "endpoint-type-not-allowed",
                $"Relation type '{relationTypeCode}' does not permit '{source.TypeId}' -> '{target.TypeId}'.");

        await EnforceCardinalityAsync(relationType, tenantId, sourceItemId, targetItemId, ct);

        var relationship = Relationship.Create(
            tenantId, projectId, sourceItemId, targetItemId, relationTypeCode, createdBy);

        await _relationships.AddAsync(relationship, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return relationship;
    }

    private async Task EnforceCardinalityAsync(
        RelationType relationType,
        TenantId tenantId,
        KnowledgeItemId sourceItemId,
        KnowledgeItemId targetItemId,
        CancellationToken ct)
    {
        if (relationType.Cardinality == RelationCardinality.ManyToMany)
            return;

        // Revoked edges never count toward cardinality.
        var sourceOutgoing = (await _relationships.ListOutgoingAsync(tenantId, sourceItemId, ct))
            .Where(r => r.Status == RelationshipStatus.Active && r.RelationTypeCode == relationType.Code)
            .ToList();
        var targetIncoming = (await _relationships.ListIncomingAsync(tenantId, targetItemId, ct))
            .Where(r => r.Status == RelationshipStatus.Active && r.RelationTypeCode == relationType.Code)
            .ToList();

        if (relationType.Cardinality == RelationCardinality.OneToOne
            && (sourceOutgoing.Count > 0 || targetIncoming.Count > 0))
            throw new RelationTypeMismatchException(
                "cardinality-violation",
                $"Relation type '{relationType.Code}' allows each item at most one edge.");

        if (relationType.Cardinality == RelationCardinality.OneToMany
            && targetIncoming.Count > 0)
            throw new RelationTypeMismatchException(
                "cardinality-violation",
                $"Relation type '{relationType.Code}' allows a target at most one incoming edge.");
    }
}