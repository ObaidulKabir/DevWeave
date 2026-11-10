using DevWeave.Application.Abstractions;
using DevWeave.Domain.Relations;

namespace DevWeave.Application.Relations;

public sealed class ListRelationTypes
{
    private readonly IRelationTypeRepository _relationTypes;

    public ListRelationTypes(IRelationTypeRepository relationTypes)
    {
        _relationTypes = relationTypes;
    }

    public Task<IReadOnlyList<RelationType>> ExecuteAsync(CancellationToken ct = default) =>
        _relationTypes.ListActiveAsync(ct);
}