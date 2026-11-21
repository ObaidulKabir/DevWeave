using DevWeave.Application.Abstractions;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Schema;

namespace DevWeave.Application.Schema;

/// <summary>
/// Registers a new schema definition (SRS SCH-001). The (Namespace, Version) pair is the registry's
/// identity: a duplicate pair is rejected with <see cref="ConflictException"/>, mirroring the
/// unique index at the persistence layer. Schemas start in Draft and must be explicitly published
/// before they can back a knowledge revision. Missing type catalogue rows are backfilled as custom
/// Knowledge-envelope types so the registry and catalogue stay aligned.
/// </summary>
public sealed class RegisterSchema
{
    private readonly ISchemaDefinitionRepository _schemas;
    private readonly IKnowledgeTypeDefinitionRepository _types;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterSchema(
        ISchemaDefinitionRepository schemas,
        IKnowledgeTypeDefinitionRepository types,
        IUnitOfWork unitOfWork)
    {
        _schemas = schemas;
        _types = types;
        _unitOfWork = unitOfWork;
    }

    public async Task<SchemaDefinition> ExecuteAsync(
        string @namespace,
        string version,
        string owner,
        string description,
        string document,
        CancellationToken ct = default)
    {
        if (await _schemas.FindAsync(@namespace, version, ct) is not null)
            throw new ConflictException($"A schema for namespace '{@namespace}' version '{version}' already exists.");

        if (await _types.GetByTypeIdAsync(@namespace, ct) is null)
        {
            await _types.AddAsync(
                KnowledgeTypeDefinition.Create(
                    @namespace,
                    owner,
                    description,
                    KnowledgeStorageKind.KnowledgeEnvelope,
                    "independent-review-verification-currency",
                    Classification.Internal,
                    isCanonical: false,
                    migrationStrategy: "custom-schema; retain-across-versions"),
                ct);
        }

        var schema = SchemaDefinition.Create(@namespace, version, owner, description, document);

        await _schemas.AddAsync(schema, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return schema;
    }
}