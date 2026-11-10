using DevWeave.Application.Abstractions;
using DevWeave.Domain.Schema;

namespace DevWeave.Application.Schema;

public sealed class GetSchema
{
    private readonly ISchemaDefinitionRepository _schemas;

    public GetSchema(ISchemaDefinitionRepository schemas)
    {
        _schemas = schemas;
    }

    public Task<SchemaDefinition?> ExecuteAsync(SchemaDefinitionId id, CancellationToken ct = default) =>
        _schemas.GetByIdAsync(id, ct);
}