using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Relations;

namespace DevWeave.Application.Relations;

public sealed class RevokeRelationship
{
    private readonly IRelationshipRepository _relationships;
    private readonly IUnitOfWork _unitOfWork;

    public RevokeRelationship(IRelationshipRepository relationships, IUnitOfWork unitOfWork)
    {
        _relationships = relationships;
        _unitOfWork = unitOfWork;
    }

    public async Task<Relationship> ExecuteAsync(TenantId tenantId, RelationshipId id, CancellationToken ct = default)
    {
        var relationship = await _relationships.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Relationship '{id}' was not found within tenant '{tenantId}'.");

        relationship.Revoke();

        await _unitOfWork.SaveChangesAsync(ct);

        return relationship;
    }
}