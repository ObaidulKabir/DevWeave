using DevWeave.Application.Abstractions;
using DevWeave.Domain.Schema;

namespace DevWeave.Application.Schema;

public sealed class ListSchemas
{
    private readonly ISchemaDefinitionRepository _schemas;

    public ListSchemas(ISchemaDefinitionRepository schemas)
    {
        _schemas = schemas;
    }

    public Task<IReadOnlyList<SchemaDefinition>> ExecuteAsync(CancellationToken ct = default) =>
        _schemas.ListAsync(ct);
}