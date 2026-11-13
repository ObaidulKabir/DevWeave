using System.Text.Json;
using DevWeave.Application.Abstractions;
using DevWeave.Application.Lifecycle;
using DevWeave.Domain.Audit;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Relations;

namespace DevWeave.Application.Relations;

/// <summary>
/// Revokes a relationship. Only an editing member of the relationship's project may do it, and who
/// revoked it, when and why is written to the audit trail in the same transaction.
/// </summary>
public sealed class RevokeRelationship
{
    public const string AuditAction = "relationship.revoked";

    private readonly IRelationshipRepository _relationships;
    private readonly IProjectMembershipRepository _memberships;
    private readonly IAuditRepository _audit;
    private readonly IUnitOfWork _unitOfWork;

    public RevokeRelationship(
        IRelationshipRepository relationships,
        IProjectMembershipRepository memberships,
        IAuditRepository audit,
        IUnitOfWork unitOfWork)
    {
        _relationships = relationships;
        _memberships = memberships;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    public async Task<Relationship> ExecuteAsync(
        TenantId tenantId, RelationshipId id, PrincipalId actor, string? reason, CancellationToken ct = default)
    {
        var relationship = await _relationships.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Relationship '{id}' was not found within tenant '{tenantId}'.");

        await ProjectWorkAuthorization.EnsureMemberAsync(
            _memberships, tenantId, relationship.ProjectId, actor, reviewerOnly: false, ct);

        var cleaned = reason?.Trim();
        if (cleaned is { Length: > 1000 })
            throw new ArgumentException("A revocation reason cannot exceed 1000 characters.", nameof(reason));

        relationship.Revoke();

        await _audit.AddAsync(
            AuditRecord.Record(
                tenantId,
                actor,
                AuditAction,
                "Relationship",
                relationship.Id.Value.ToString(),
                JsonSerializer.Serialize(new
                {
                    projectId = relationship.ProjectId.Value,
                    sourceItemId = relationship.SourceItemId.Value,
                    targetItemId = relationship.TargetItemId.Value,
                    relationType = relationship.RelationTypeCode,
                    reason = string.IsNullOrEmpty(cleaned) ? null : cleaned,
                })),
            ct);

        await _unitOfWork.SaveChangesAsync(ct);

        return relationship;
    }
}
