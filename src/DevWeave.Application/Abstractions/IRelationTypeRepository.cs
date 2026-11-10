using DevWeave.Domain.Relations;

namespace DevWeave.Application.Abstractions;

/// <summary>
/// Persistence port for platform-global relation types (SRS REL-002, REL-003). Like schema
/// definitions, relation types are platform configuration, so reads are not tenant-scoped.
/// </summary>
public interface IRelationTypeRepository
{
    Task AddAsync(RelationType relationType, CancellationToken ct = default);

    Task<RelationType?> GetByCodeAsync(string code, CancellationToken ct = default);

    Task<RelationType?> GetByIdAsync(RelationTypeId id, CancellationToken ct = default);

    Task<IReadOnlyList<RelationType>> ListActiveAsync(CancellationToken ct = default);
}