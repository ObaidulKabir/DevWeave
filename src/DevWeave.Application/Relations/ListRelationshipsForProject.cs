using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Relations;

namespace DevWeave.Application.Relations;

public sealed class ListRelationshipsForProject
{
    private readonly IRelationshipRepository _relationships;
    private readonly RelationshipReadAuthorization _authorization;

    public ListRelationshipsForProject(
        IRelationshipRepository relationships,
        RelationshipReadAuthorization authorization)
    {
        _relationships = relationships;
        _authorization = authorization;
    }

    public async Task<IReadOnlyList<Relationship>> ExecuteAsync(
        TenantId tenantId,
        ProjectId projectId,
        PrincipalId actorId,
        CancellationToken ct = default)
    {
        var role = await _authorization.ResolveRoleAsync(tenantId, projectId, actorId, ct);
        if (role is null)
            return [];

        var relationships = await _relationships.ListByProjectAsync(tenantId, projectId, ct);
        return await _authorization.FilterProjectRelationshipsAsync(
            tenantId,
            projectId,
            role.Value,
            relationships,
            ct);
    }
}