using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Planning;

public sealed class GetRequirementChangeSet
{
    private readonly IRequirementChangeSetRepository _changeSets;

    public GetRequirementChangeSet(IRequirementChangeSetRepository changeSets)
    {
        _changeSets = changeSets;
    }

    public async Task<RequirementChangeSet> ExecuteAsync(
        TenantId tenantId,
        ProjectId projectId,
        RequirementChangeSetId id,
        CancellationToken ct = default)
    {
        var changeSet = await _changeSets.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Requirement change set '{id}' was not found.");
        if (changeSet.ProjectId != projectId)
            throw new NotFoundException($"Requirement change set '{id}' was not found.");
        return changeSet;
    }
}
