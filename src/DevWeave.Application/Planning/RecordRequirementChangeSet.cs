using DevWeave.Application.Abstractions;
using DevWeave.Application.Lifecycle;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Planning;

public sealed class RecordRequirementChangeSet
{
    private readonly IRequirementChangeSetRepository _changeSets;
    private readonly IProjectMembershipRepository _memberships;
    private readonly IUnitOfWork _unitOfWork;

    public RecordRequirementChangeSet(
        IRequirementChangeSetRepository changeSets,
        IProjectMembershipRepository memberships,
        IUnitOfWork unitOfWork)
    {
        _changeSets = changeSets;
        _memberships = memberships;
        _unitOfWork = unitOfWork;
    }

    public async Task<RequirementChangeSet> ExecuteAsync(
        TenantId tenantId,
        ProjectId projectId,
        PrincipalId recordedBy,
        IReadOnlyList<RequirementChangeDescriptor> changes,
        string? correlationId = null,
        CancellationToken ct = default)
    {
        await ProjectWorkAuthorization.EnsureMemberAsync(
            _memberships, tenantId, projectId, recordedBy, reviewerOnly: false, ct);

        var changeSet = RequirementChangeSet.Create(tenantId, projectId, recordedBy, changes, correlationId);
        await _changeSets.AddAsync(changeSet, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return changeSet;
    }
}
