using DevWeave.Application.Abstractions;
using DevWeave.Domain.Relations;

namespace DevWeave.Application.Relations;

public sealed class DeprecateRelationType
{
    private readonly IRelationTypeRepository _relationTypes;
    private readonly IUnitOfWork _unitOfWork;

    public DeprecateRelationType(IRelationTypeRepository relationTypes, IUnitOfWork unitOfWork)
    {
        _relationTypes = relationTypes;
        _unitOfWork = unitOfWork;
    }

    public async Task<RelationType> ExecuteAsync(RelationTypeId id, CancellationToken ct = default)
    {
        var relationType = await _relationTypes.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Relation type '{id}' was not found.");

        relationType.Deprecate();

        await _unitOfWork.SaveChangesAsync(ct);

        return relationType;
    }
}