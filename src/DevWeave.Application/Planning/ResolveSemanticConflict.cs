using DevWeave.Application.Abstractions;
using DevWeave.Application.Lifecycle;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Planning;

public sealed class ResolveSemanticConflict
{
    private readonly ISemanticConflictRepository _conflicts;
    private readonly IProjectMembershipRepository _memberships;
    private readonly IUnitOfWork _unitOfWork;

    public ResolveSemanticConflict(
        ISemanticConflictRepository conflicts,
        IProjectMembershipRepository memberships,
        IUnitOfWork unitOfWork)
    {
        _conflicts = conflicts;
        _memberships = memberships;
        _unitOfWork = unitOfWork;
    }

    public async Task<SemanticConflict> ExecuteAsync(
        TenantId tenantId,
        ProjectId projectId,
        SemanticConflictId conflictId,
        SemanticConflictResolutionKind resolution,
        string reason,
        PrincipalId resolvedBy,
        CancellationToken ct = default)
    {
        var conflict = await _conflicts.GetByIdAsync(tenantId, conflictId, ct)
            ?? throw new NotFoundException($"Semantic conflict '{conflictId}' was not found.");
        if (conflict.ProjectId != projectId)
            throw new NotFoundException($"Semantic conflict '{conflictId}' was not found.");

        await ProjectWorkAuthorization.EnsureMemberAsync(
            _memberships, tenantId, projectId, resolvedBy, reviewerOnly: true, ct);

        try
        {
            conflict.Resolve(resolution, resolvedBy, reason);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return conflict;
    }
}
