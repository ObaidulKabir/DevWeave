using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using EvidenceRecord = DevWeave.Domain.Evidence.Evidence;

namespace DevWeave.Application.Evidence;

/// <summary>Lists the evidence recorded for a project, tenant-scoped (deny-by-default, SRS PRO-006).</summary>
public sealed class ListEvidenceForProject
{
    private readonly IEvidenceRepository _evidence;

    public ListEvidenceForProject(IEvidenceRepository evidence)
    {
        _evidence = evidence;
    }

    public Task<IReadOnlyList<EvidenceRecord>> ExecuteAsync(
        TenantId tenantId,
        ProjectId projectId,
        CancellationToken ct = default) =>
        _evidence.ListForProjectAsync(tenantId, projectId, ct);
}