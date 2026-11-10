using DevWeave.Application.Abstractions;
using DevWeave.Domain.Schema;

namespace DevWeave.Application.Schema;

/// <summary>
/// Publishes a draft schema so it becomes the active target for knowledge revisions (SRS SCH-003).
/// </summary>
public sealed class PublishSchema
{
    private readonly ISchemaDefinitionRepository _schemas;
    private readonly IUnitOfWork _unitOfWork;

    public PublishSchema(ISchemaDefinitionRepository schemas, IUnitOfWork unitOfWork)
    {
        _schemas = schemas;
        _unitOfWork = unitOfWork;
    }

    public async Task<SchemaDefinition> ExecuteAsync(SchemaDefinitionId id, CancellationToken ct = default)
    {
        var schema = await _schemas.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Schema '{id}' was not found.");

        schema.Publish();

        await _unitOfWork.SaveChangesAsync(ct);

        return schema;
    }
}