using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;

namespace DevWeave.Application.Knowledge;

public sealed class GetRevision
{
    private readonly IKnowledgeRevisionRepository _revisions;

    public GetRevision(IKnowledgeRevisionRepository revisions)
    {
        _revisions = revisions;
    }

    public Task<KnowledgeRevision?> ExecuteAsync(TenantId tenantId, KnowledgeRevisionId id, CancellationToken ct = default) =>
        _revisions.GetByIdAsync(tenantId, id, ct);
}