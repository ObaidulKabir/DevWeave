using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevWeave.Infrastructure.Repositories;

internal sealed class PlanDesignRepository(DevWeaveDbContext db) : IPlanDesignRepository
{
    public Task<PlanOutlineNode?> GetNodeAsync(TenantId tenant, ProjectId project, KnowledgeItemId item, CancellationToken ct) =>
        db.PlanOutlineNodes.SingleOrDefaultAsync(n => n.TenantId == tenant && n.ProjectId == project && n.ItemId == item, ct);

    public async Task<IReadOnlyList<PlanOutlineNode>> ListNodesAsync(TenantId tenant, ProjectId project, CancellationToken ct) =>
        await db.PlanOutlineNodes.AsNoTracking().Where(n => n.TenantId == tenant && n.ProjectId == project)
            .OrderBy(n => n.Position).ThenBy(n => n.CreatedAt).ToListAsync(ct);

    public Task AddNodeAsync(PlanOutlineNode node, CancellationToken ct)
    {
        db.PlanOutlineNodes.Add(node);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<PlanWorkLink>> ListLinksAsync(TenantId tenant, ProjectId project, CancellationToken ct) =>
        await db.PlanWorkLinks.AsNoTracking().Where(l => l.TenantId == tenant && l.ProjectId == project)
            .OrderBy(l => l.CreatedAt).ToListAsync(ct);

    public Task AddLinkAsync(PlanWorkLink link, CancellationToken ct)
    {
        db.PlanWorkLinks.Add(link);
        return Task.CompletedTask;
    }

    public Task<PlanExpansionProposal?> GetProposalAsync(TenantId tenant, ProjectId project, PlanExpansionProposalId id, CancellationToken ct) =>
        db.PlanExpansionProposals.SingleOrDefaultAsync(p => p.TenantId == tenant && p.ProjectId == project && p.Id == id, ct);

    public async Task<IReadOnlyList<PlanExpansionProposal>> ListProposalsAsync(TenantId tenant, ProjectId project, CancellationToken ct) =>
        await db.PlanExpansionProposals.AsNoTracking().Where(p => p.TenantId == tenant && p.ProjectId == project)
            .OrderBy(p => p.ProposedAt).ToListAsync(ct);

    public Task AddProposalAsync(PlanExpansionProposal proposal, CancellationToken ct)
    {
        db.PlanExpansionProposals.Add(proposal);
        return Task.CompletedTask;
    }
}
