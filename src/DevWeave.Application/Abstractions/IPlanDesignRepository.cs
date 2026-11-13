using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Abstractions;

public interface IPlanDesignRepository
{
    Task<PlanOutlineNode?> GetNodeAsync(TenantId tenant, ProjectId project, KnowledgeItemId item, CancellationToken ct);
    Task<IReadOnlyList<PlanOutlineNode>> ListNodesAsync(TenantId tenant, ProjectId project, CancellationToken ct);
    Task AddNodeAsync(PlanOutlineNode node, CancellationToken ct);
    Task RemoveNodeAsync(PlanOutlineNode node, CancellationToken ct);
    Task<IReadOnlyList<PlanWorkLink>> ListLinksAsync(TenantId tenant, ProjectId project, CancellationToken ct);
    Task AddLinkAsync(PlanWorkLink link, CancellationToken ct);
    Task RemoveLinkAsync(PlanWorkLink link, CancellationToken ct);
    Task<PlanExpansionProposal?> GetProposalAsync(TenantId tenant, ProjectId project, PlanExpansionProposalId id, CancellationToken ct);
    Task<IReadOnlyList<PlanExpansionProposal>> ListProposalsAsync(TenantId tenant, ProjectId project, CancellationToken ct);
    Task AddProposalAsync(PlanExpansionProposal proposal, CancellationToken ct);
}
