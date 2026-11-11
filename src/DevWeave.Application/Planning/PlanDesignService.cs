using System.Text;
using System.Text.Json;
using DevWeave.Application.Abstractions;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Planning;

public sealed record PlanOutlineView(
    IReadOnlyList<PlanOutlineNode> Nodes,
    IReadOnlyList<PlanWorkLink> Links,
    IReadOnlyList<PlanExpansionProposal> Proposals);

public sealed class PlanDesignService(
    IPlanDesignRepository plans,
    IKnowledgeItemRepository items,
    IKnowledgeRevisionRepository revisions,
    IWorkPlanningRepository work,
    IProjectRepository projects,
    IPrincipalRepository principals,
    IProjectMembershipRepository memberships,
    IAgentGrantRepository grants,
    IAgentCredentialRepository credentials,
    IContentInspector inspector,
    IUnitOfWork uow)
{
    public async Task<PlanOutlineView> ReadAsync(TenantId tenant, ProjectId project, PrincipalId actor,
        CancellationToken ct = default)
    {
        var (principal, ceiling) = await AuthorizeAsync(tenant, project, actor, write: false, review: false, ct);
        var nodes = await plans.ListNodesAsync(tenant, project, ct);
        var links = await plans.ListLinksAsync(tenant, project, ct);
        var proposals = await plans.ListProposalsAsync(tenant, project, ct);
        if (principal.Type == PrincipalType.Human)
            return new(nodes, links, proposals);

        // Do not disclose even the identity of a classified ancestor or its descendants.
        var visible = new HashSet<KnowledgeItemId>();
        var byId = nodes.ToDictionary(n => n.ItemId);
        foreach (var node in nodes)
        {
            var item = await items.GetByIdAsync(tenant, node.ItemId, ct);
            if (item is null || item.Classification > ceiling)
                continue;
            var parent = node.ParentItemId;
            var ancestorsVisible = true;
            while (parent is not null)
            {
                if (!byId.TryGetValue(parent, out var ancestor))
                {
                    ancestorsVisible = false;
                    break;
                }
                var ancestorItem = await items.GetByIdAsync(tenant, parent, ct);
                if (ancestorItem is null || ancestorItem.Classification > ceiling)
                {
                    ancestorsVisible = false;
                    break;
                }
                parent = ancestor.ParentItemId;
            }
            if (ancestorsVisible) visible.Add(node.ItemId);
        }
        return new(
            nodes.Where(n => visible.Contains(n.ItemId)).ToArray(),
            links.Where(l => visible.Contains(l.KnowledgeItemId)).ToArray(),
            proposals.Where(p => p.ProposedBy == actor && visible.Contains(p.ParentItemId)).ToArray());
    }

    public async Task<PlanOutlineNode> AttachAsync(TenantId tenant, ProjectId project, PrincipalId actor,
        KnowledgeItemId itemId, KnowledgeItemId? parentId, int position, CancellationToken ct = default)
    {
        await EnsureHumanWriterAsync(tenant, project, actor, ct);
        var item = await RequireItemAsync(tenant, project, itemId, ct);
        if (item.CurrentRevisionId is null)
            throw new ConflictException("Outline items need a committed Knowledge revision.");
        if (await plans.GetNodeAsync(tenant, project, itemId, ct) is not null)
            throw new ConflictException("Knowledge item already belongs to the outline.");

        var nodes = await plans.ListNodesAsync(tenant, project, ct);
        var root = nodes.SingleOrDefault(n => n.ParentItemId is null);
        PlanOutlineNode? parent = null;
        if (parentId is null)
        {
            if (nodes.Count != 0)
                throw new ConflictException("A project has only one outline root.");
        }
        else
        {
            parent = await plans.GetNodeAsync(tenant, project, parentId, ct)
                ?? throw new NotFoundException("Parent is not in this project's outline.");
            if (root is null || parent.RootItemId != root.ItemId)
                throw new ConflictException("Outline parent is not connected to the project root.");
        }
        var node = PlanOutlineNode.Create(tenant, project, itemId, parent?.RootItemId ?? itemId, parentId, position, actor);
        await plans.AddNodeAsync(node, ct);
        await uow.SaveChangesAsync(ct);
        return node;
    }

    public async Task<PlanWorkLink> LinkAsync(TenantId tenant, ProjectId project, PrincipalId actor,
        WorkItemId workId, KnowledgeItemId itemId, KnowledgeRevisionId revisionId, CancellationToken ct = default)
    {
        await EnsureHumanWriterAsync(tenant, project, actor, ct);
        _ = await work.GetWorkItemAsync(tenant, project, workId, ct)
            ?? throw new NotFoundException("Work item was not found in this project.");
        var item = await RequireItemAsync(tenant, project, itemId, ct);
        var revision = await revisions.GetByIdAsync(tenant, revisionId, ct);
        if (revision is null || revision.KnowledgeItemId != itemId)
            throw new NotFoundException("Revision does not belong to this Knowledge item.");
        if (item.CurrentRevisionId != revisionId || item.ReviewState != ReviewState.Reviewed)
            throw new ConflictException("Work links require the current human-reviewed Knowledge revision.");
        if (await plans.GetNodeAsync(tenant, project, itemId, ct) is null)
            throw new ConflictException("Knowledge item must belong to the project outline.");
        if ((await plans.ListLinksAsync(tenant, project, ct)).Any(l => l.WorkItemId == workId && l.RevisionId == revisionId))
            throw new ConflictException("This revision is already linked to the work item.");
        var link = PlanWorkLink.Create(tenant, project, workId, itemId, revisionId, actor);
        await plans.AddLinkAsync(link, ct);
        await uow.SaveChangesAsync(ct);
        return link;
    }

    public async Task<PlanExpansionProposal> ProposeAsync(TenantId tenant, ProjectId project, PrincipalId actor,
        KnowledgeItemId parentId, string key, string title, string typeId, string rationale, CancellationToken ct = default)
    {
        var (principal, ceiling) = await AuthorizeAsync(tenant, project, actor, write: true, review: false, ct);
        var parent = await RequireItemAsync(tenant, project, parentId, ct);
        if (await plans.GetNodeAsync(tenant, project, parentId, ct) is null || parent.Classification > ceiling)
            throw new NotFoundException("Parent is not accessible in the outline.");
        if (principal.Type == PrincipalType.AiAgent)
        {
            var ancestor = (await plans.GetNodeAsync(tenant, project, parentId, ct))!.ParentItemId;
            while (ancestor is not null)
            {
                var ancestorItem = await RequireItemAsync(tenant, project, ancestor, ct);
                if (ancestorItem.Classification > ceiling)
                    throw new NotFoundException("Parent is not accessible in the outline.");
                ancestor = (await plans.GetNodeAsync(tenant, project, ancestor, ct))?.ParentItemId;
            }
        }
        var proposal = PlanExpansionProposal.Propose(tenant, project, parentId, key, title, typeId, rationale, actor);
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { proposal.Key, proposal.Title, proposal.TypeId, proposal.Rationale }));
        var inspection = await inspector.InspectAsync("", "application/json", bytes, ct);
        if (inspection.Verdict != ContentVerdict.Allow)
            throw new ProhibitedContentException("Proposal was rejected by content policy.", inspection.Reasons);
        if (principal.Type == PrincipalType.AiAgent && parent.ReviewState != ReviewState.Reviewed)
            throw new ConflictException("Agents may expand only human-reviewed Knowledge.");
        await plans.AddProposalAsync(proposal, ct);
        await uow.SaveChangesAsync(ct);
        return proposal;
    }

    public async Task<PlanExpansionProposal> DecideAsync(TenantId tenant, ProjectId project, PrincipalId actor,
        PlanExpansionProposalId proposalId, KnowledgeItemId? acceptedItem, CancellationToken ct = default)
    {
        await AuthorizeAsync(tenant, project, actor, write: true, review: true, ct);
        var proposal = await plans.GetProposalAsync(tenant, project, proposalId, ct)
            ?? throw new NotFoundException("Proposal was not found in this project.");
        if (proposal.Status != PlanProposalStatus.Proposed)
            throw new ConflictException("Proposal has already been decided.");
        if (proposal.ProposedBy == actor)
            throw new ConflictException("A proposer cannot review their own expansion.");
        if (acceptedItem is not null)
        {
            var item = await RequireItemAsync(tenant, project, acceptedItem, ct);
            if (item.Key != proposal.Key || item.Title != proposal.Title || item.TypeId != proposal.TypeId ||
                item.CurrentRevisionId is null || item.ReviewState != ReviewState.Reviewed)
                throw new ConflictException("Accepted item must match the proposal and have a human-reviewed revision.");
            if (await plans.GetNodeAsync(tenant, project, proposal.ParentItemId, ct) is null)
                throw new ConflictException("Proposal parent is no longer in the outline.");
            if (await plans.GetNodeAsync(tenant, project, acceptedItem, ct) is not null)
                throw new ConflictException("Accepted item already belongs to the outline.");
            var parent = (await plans.GetNodeAsync(tenant, project, proposal.ParentItemId, ct))!;
            await plans.AddNodeAsync(PlanOutlineNode.Create(tenant, project, acceptedItem,
                parent.RootItemId, parent.ItemId, (await plans.ListNodesAsync(tenant, project, ct)).Count, actor), ct);
        }
        proposal.Decide(actor, acceptedItem);
        await uow.SaveChangesAsync(ct);
        return proposal;
    }

    private async Task<KnowledgeItem> RequireItemAsync(TenantId tenant, ProjectId project, KnowledgeItemId id, CancellationToken ct)
    {
        var item = await items.GetByIdAsync(tenant, id, ct);
        return item is not null && item.ProjectId == project
            ? item : throw new NotFoundException("Knowledge item was not found in this project.");
    }

    private async Task EnsureHumanWriterAsync(TenantId tenant, ProjectId project, PrincipalId actor, CancellationToken ct)
    {
        var (principal, _) = await AuthorizeAsync(tenant, project, actor, write: true, review: false, ct);
        if (principal.Type != PrincipalType.Human)
            throw new UnauthorizedTenantException("Only humans can attach Knowledge or pin Work revisions.");
    }

    private async Task<(Principal Principal, Classification Ceiling)> AuthorizeAsync(TenantId tenant, ProjectId project,
        PrincipalId actor, bool write, bool review, CancellationToken ct)
    {
        _ = await projects.GetByIdAsync(tenant, project, ct)
            ?? throw new NotFoundException("Project was not found.");
        var principal = await principals.GetByIdAsync(tenant, actor, ct);
        if (principal is null || !principal.IsActive)
            throw new NotFoundException("Actor was not found.");
        var member = (await memberships.ListByPrincipalAsync(tenant, actor, ct))
            .FirstOrDefault(m => m.ProjectId == project && m.Status == ProjectMembershipStatus.Active)
            ?? throw new NotFoundException("Project was not found.");
        if (principal.Type == PrincipalType.Human)
        {
            if ((write && member.Role == ProjectRole.Viewer) ||
                (review && member.Role is not (ProjectRole.Owner or ProjectRole.Admin)))
                throw new NotFoundException("Project was not found.");
            return (principal, Classification.Restricted);
        }
        if (principal.Type != PrincipalType.AiAgent || review)
            throw new UnauthorizedTenantException("Actor cannot review planning proposals.");
        if (!(await credentials.ListForAgentAsync(tenant, actor, ct)).Any(c => c.IsActive(DateTimeOffset.UtcNow)))
            throw new UnauthorizedTenantException("Agent has no active credential.");
        var authorized = (await grants.ListForAgentAsync(tenant, actor, ct))
            .Where(g => g.IsActive(DateTimeOffset.UtcNow) && g.CoversProject(project) &&
                        g.Capability == (write ? AgentCapability.WriteKnowledge : AgentCapability.ReadKnowledge))
            .ToArray();
        if (authorized.Length == 0)
            throw new UnauthorizedTenantException("Agent lacks a project planning capability.");
        return (principal, authorized.Max(g => g.ClassificationCeiling));
    }
}
