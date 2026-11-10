using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Relations;

namespace DevWeave.Application.Relations;

public sealed class GetRelationship
{
    private readonly IRelationshipRepository _relationships;

    public GetRelationship(IRelationshipRepository relationships)
    {
        _relationships = relationships;
    }

    public Task<Relationship?> ExecuteAsync(TenantId tenantId, RelationshipId id, CancellationToken ct = default) =>
        _relationships.GetByIdAsync(tenantId, id, ct);
}