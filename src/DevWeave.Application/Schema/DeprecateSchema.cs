using DevWeave.Application.Abstractions;
using DevWeave.Domain.Schema;

namespace DevWeave.Application.Schema;

/// <summary>
/// Deprecates a schema so it stops being listed as active and can no longer back new revisions
/// (SRS SCH-003).
/// </summary>
public sealed class DeprecateSchema
{
    private readonly ISchemaDefinitionRepository _schemas;
    private readonly IUnitOfWork _unitOfWork;

    public DeprecateSchema(ISchemaDefinitionRepository schemas, IUnitOfWork unitOfWork)
    {
        _schemas = schemas;
        _unitOfWork = unitOfWork;
    }

    public async Task<SchemaDefinition> ExecuteAsync(SchemaDefinitionId id, CancellationToken ct = default)
    {
        var schema = await _schemas.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Schema '{id}' was not found.");

        schema.Deprecate();

        await _unitOfWork.SaveChangesAsync(ct);

        return schema;
    }
}