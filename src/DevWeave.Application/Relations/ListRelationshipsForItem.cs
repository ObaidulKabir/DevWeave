using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Relations;

namespace DevWeave.Application.Relations;

/// <summary>
/// Lists every relationship in which an item participates (as source or target), in traversal-stable
/// order. The item must be resolvable inside the tenant (deny-by-default); otherwise the read is
/// indistinguishable from an empty result.
/// </summary>
public sealed class ListRelationshipsForItem
{
    private readonly IRelationshipRepository _relationships;
    private readonly RelationshipReadAuthorization _authorization;

    public ListRelationshipsForItem(
        IRelationshipRepository relationships,
        RelationshipReadAuthorization authorization)
    {
        _relationships = relationships;
        _authorization = authorization;
    }

    public async Task<IReadOnlyList<Relationship>> ExecuteAsync(
        TenantId tenantId,
        KnowledgeItemId itemId,
        PrincipalId actorId,
        CancellationToken ct = default)
    {
        var (_, role) = await _authorization.RequireVisibleItemAsync(tenantId, itemId, actorId, ct);

        var outgoing = await _relationships.ListOutgoingAsync(tenantId, itemId, ct);
        var incoming = await _relationships.ListIncomingAsync(tenantId, itemId, ct);
        var candidates = outgoing
            .Concat(incoming)
            .GroupBy(relationship => relationship.Id)
            .Select(group => group.First())
            .ToList();

        var visible = new List<Relationship>();
        foreach (var relationship in candidates)
        {
            if (await _authorization.IsRelationshipVisibleAsync(tenantId, relationship, role, ct))
                visible.Add(relationship);
        }

        return visible
            .OrderBy(r => r.CreatedAt)
            .ThenBy(r => r.Id.Value)
            .ToList();
    }
}