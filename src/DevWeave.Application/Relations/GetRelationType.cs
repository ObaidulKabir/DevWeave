using DevWeave.Application.Abstractions;
using DevWeave.Domain.Relations;

namespace DevWeave.Application.Relations;

public sealed class GetRelationType
{
    private readonly IRelationTypeRepository _relationTypes;

    public GetRelationType(IRelationTypeRepository relationTypes)
    {
        _relationTypes = relationTypes;
    }

    public Task<RelationType?> ExecuteAsync(RelationTypeId id, CancellationToken ct = default) =>
        _relationTypes.GetByIdAsync(id, ct);
}