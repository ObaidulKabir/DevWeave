using DevWeave.Domain.Schema;

namespace DevWeave.Application.Abstractions;

/// <summary>
/// Persistence port for platform-global schema definitions (SRS SCH-001..SCH-003, KNW-008). Unlike
/// tenant-owned aggregates, schema definitions are platform configuration, so reads are not
/// tenant-scoped.
/// </summary>
public interface ISchemaDefinitionRepository
{
    Task AddAsync(SchemaDefinition schema, CancellationToken ct = default);

    Task<SchemaDefinition?> FindAsync(string @namespace, string version, CancellationToken ct = default);

    Task<SchemaDefinition?> GetByIdAsync(SchemaDefinitionId id, CancellationToken ct = default);

    Task<IReadOnlyList<SchemaDefinition>> ListAsync(CancellationToken ct = default);
}