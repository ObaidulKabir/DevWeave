using DevWeave.Application.Abstractions;
using DevWeave.Domain.Schema;

namespace DevWeave.Application.Schema;

public sealed class ListKnowledgeTypes(IKnowledgeTypeDefinitionRepository types)
{
    public Task<IReadOnlyList<KnowledgeTypeDefinition>> ExecuteAsync(CancellationToken ct = default) =>
        types.ListAsync(ct);
}
