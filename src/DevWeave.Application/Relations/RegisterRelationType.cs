using DevWeave.Application.Abstractions;
using DevWeave.Domain.Relations;

namespace DevWeave.Application.Relations;

/// <summary>
/// Registers a new platform-global relation type (SRS REL-002). Duplicate codes are rejected with
/// <see cref="ConflictException"/>, mirroring the unique index at the persistence layer.
/// </summary>
public sealed class RegisterRelationType
{
    private readonly IRelationTypeRepository _relationTypes;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterRelationType(IRelationTypeRepository relationTypes, IUnitOfWork unitOfWork)
    {
        _relationTypes = relationTypes;
        _unitOfWork = unitOfWork;
    }

    public async Task<RelationType> ExecuteAsync(
        string code,
        string name,
        IReadOnlyCollection<string>? allowedSourceTypeIds,
        IReadOnlyCollection<string>? allowedTargetTypeIds,
        RelationDirection direction,
        RelationCardinality cardinality,
        CancellationToken ct = default)
    {
        if (await _relationTypes.GetByCodeAsync(code, ct) is not null)
            throw new ConflictException($"A relation type with code '{code}' already exists.");

        var relationType = RelationType.Create(
            code, name, allowedSourceTypeIds, allowedTargetTypeIds, direction, cardinality);

        await _relationTypes.AddAsync(relationType, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return relationType;
    }
}