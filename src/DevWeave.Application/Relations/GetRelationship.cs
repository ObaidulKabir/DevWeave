using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Relations;

namespace DevWeave.Application.Relations;

public sealed class GetRelationship
{
    private readonly IRelationshipRepository _relationships;
    private readonly RelationshipReadAuthorization _authorization;

    public GetRelationship(
        IRelationshipRepository relationships,
        RelationshipReadAuthorization authorization)
    {
        _relationships = relationships;
        _authorization = authorization;
    }

    public async Task<Relationship?> ExecuteAsync(
        TenantId tenantId,
        RelationshipId id,
        PrincipalId actorId,
        CancellationToken ct = default)
    {
        var relationship = await _relationships.GetByIdAsync(tenantId, id, ct);
        if (relationship is null)
            return null;

        var role = await _authorization.ResolveRoleAsync(
            tenantId,
            relationship.ProjectId,
            actorId,
            ct);

        return role is not null &&
               await _authorization.IsRelationshipVisibleAsync(tenantId, relationship, role.Value, ct)
            ? relationship
            : null;
    }
}