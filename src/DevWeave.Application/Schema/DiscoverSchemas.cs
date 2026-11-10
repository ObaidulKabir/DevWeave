using DevWeave.Application.Abstractions;
using DevWeave.Domain.Schema;

namespace DevWeave.Application.Schema;

/// <summary>
/// Discovers the active (published) schemas exposed to authorized clients and agents (SRS KNW-008).
/// Only Published schemas are discoverable; Draft and Deprecated schemas are hidden.
/// </summary>
public sealed class DiscoverSchemas
{
    private readonly ISchemaDefinitionRepository _schemas;

    public DiscoverSchemas(ISchemaDefinitionRepository schemas)
    {
        _schemas = schemas;
    }

    public async Task<IReadOnlyList<SchemaDefinition>> ExecuteAsync(CancellationToken ct = default)
    {
        var schemas = await _schemas.ListAsync(ct);

        return schemas.Where(s => s.IsActive).ToList();
    }
}