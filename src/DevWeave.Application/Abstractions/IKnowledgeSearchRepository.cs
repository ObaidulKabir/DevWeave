using DevWeave.Contracts.Knowledge.V1;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Abstractions;

/// <summary>
/// Searches only after the application has verified active project membership. Every query must
/// retain the tenant, project, and classification ceiling in its database predicates.
/// </summary>
public interface IKnowledgeSearchRepository
{
    Task<SearchKnowledgeResponse> SearchAsync(
        TenantId tenantId,
        ProjectId projectId,
        Classification classificationCeiling,
        SearchKnowledgeRequest request,
        CancellationToken ct = default);
}
