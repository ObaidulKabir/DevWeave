using DevWeave.Application.Abstractions;
using DevWeave.Application.Lifecycle;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Planning;

public sealed class RecordSemanticConflict
{
    private readonly ISemanticConflictRepository _conflicts;
    private readonly IKnowledgeRevisionRepository _revisions;
    private readonly IProjectMembershipRepository _memberships;
    private readonly IUnitOfWork _unitOfWork;

    public RecordSemanticConflict(
        ISemanticConflictRepository conflicts,
        IKnowledgeRevisionRepository revisions,
        IProjectMembershipRepository memberships,
        IUnitOfWork unitOfWork)
    {
        _conflicts = conflicts;
        _revisions = revisions;
        _memberships = memberships;
        _unitOfWork = unitOfWork;
    }

    public async Task<SemanticConflict> ExecuteAsync(
        TenantId tenantId,
        ProjectId projectId,
        KnowledgeRevisionId leftRevisionId,
        KnowledgeRevisionId rightRevisionId,
        string reason,
        PrincipalId recordedBy,
        CancellationToken ct = default)
    {
        await ProjectWorkAuthorization.EnsureMemberAsync(
            _memberships, tenantId, projectId, recordedBy, reviewerOnly: false, ct);

        _ = await _revisions.GetByIdAsync(tenantId, leftRevisionId, ct)
            ?? throw new NotFoundException($"Knowledge revision '{leftRevisionId}' was not found.");
        _ = await _revisions.GetByIdAsync(tenantId, rightRevisionId, ct)
            ?? throw new NotFoundException($"Knowledge revision '{rightRevisionId}' was not found.");

        var conflict = SemanticConflict.Record(
            tenantId, projectId, leftRevisionId, rightRevisionId, reason, recordedBy);
        await _conflicts.AddAsync(conflict, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return conflict;
    }
}
