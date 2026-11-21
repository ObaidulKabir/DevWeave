using DevWeave.Domain.Schema;

namespace DevWeave.Application.Abstractions;

public interface IKnowledgeTypeDefinitionRepository
{
    Task<KnowledgeTypeDefinition?> GetByTypeIdAsync(string typeId, CancellationToken ct = default);

    Task<IReadOnlyList<KnowledgeTypeDefinition>> ListAsync(CancellationToken ct = default);

    Task AddAsync(KnowledgeTypeDefinition definition, CancellationToken ct = default);
}
