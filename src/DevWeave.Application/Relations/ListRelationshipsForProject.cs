using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Relations;

namespace DevWeave.Application.Relations;

public sealed class ListRelationshipsForProject
{
    private readonly IRelationshipRepository _relationships;

    public ListRelationshipsForProject(IRelationshipRepository relationships)
    {
        _relationships = relationships;
    }

    public Task<IReadOnlyList<Relationship>> ExecuteAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default) =>
        _relationships.ListByProjectAsync(tenantId, projectId, ct);
}