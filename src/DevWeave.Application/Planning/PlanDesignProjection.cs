using DevWeave.Domain.Planning;

namespace DevWeave.Application.Planning;

public static class PlanDesignProjection
{
    public static object Outline(PlanOutlineView view) => new
    {
        nodes = view.Nodes.Select(Node).ToArray(),
        links = view.Links.Select(Link).ToArray(),
        proposals = view.Proposals.Select(Proposal).ToArray(),
    };

    public static object Node(PlanOutlineNode node) => new
    {
        itemId = node.ItemId.Value,
        rootItemId = node.RootItemId.Value,
        parentItemId = node.ParentItemId?.Value,
        node.Position,
        createdBy = node.CreatedBy.Value,
        node.CreatedAt,
    };

    public static object Link(PlanWorkLink link) => new
    {
        id = link.Id.Value,
        workItemId = link.WorkItemId.Value,
        knowledgeItemId = link.KnowledgeItemId.Value,
        revisionId = link.RevisionId.Value,
        createdBy = link.CreatedBy.Value,
        link.CreatedAt,
    };

    public static object Proposal(PlanExpansionProposal proposal) => new
    {
        id = proposal.Id.Value,
        parentItemId = proposal.ParentItemId.Value,
        proposal.Key,
        proposal.Title,
        proposal.TypeId,
        proposal.Rationale,
        proposedBy = proposal.ProposedBy.Value,
        status = proposal.Status.ToString(),
        resolvedItemId = proposal.ResolvedItemId?.Value,
        reviewedBy = proposal.ReviewedBy?.Value,
        proposal.ReviewedAt,
    };
}
