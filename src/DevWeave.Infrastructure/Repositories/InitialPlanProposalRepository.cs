using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevWeave.Infrastructure.Repositories;

public sealed class InitialPlanProposalRepository(DevWeaveDbContext db) : IInitialPlanProposalRepository
{
    public Task<InitialPlanProposal?> GetAsync(TenantId tenant, ProjectId project, InitialPlanProposalId id, CancellationToken ct) =>
        db.InitialPlanProposals.SingleOrDefaultAsync(
            p => p.TenantId == tenant && p.ProjectId == project && p.Id == id, ct);

    public Task<InitialPlanProposal?> FindByIdempotencyAsync(
        TenantId tenant, ProjectId project, PrincipalId proposer, string idempotencyKey, CancellationToken ct) =>
        db.InitialPlanProposals.SingleOrDefaultAsync(
            p => p.TenantId == tenant && p.ProjectId == project && p.ProposedBy == proposer && p.IdempotencyKey == idempotencyKey, ct);

    public async Task<IReadOnlyList<InitialPlanProposal>> ListAsync(TenantId tenant, ProjectId project, CancellationToken ct) =>
        await db.InitialPlanProposals.AsNoTracking()
            .Where(p => p.TenantId == tenant && p.ProjectId == project)
            .OrderByDescending(p => p.ProposedAt)
            .ToListAsync(ct);

    public Task AddAsync(InitialPlanProposal proposal, CancellationToken ct)
    {
        db.InitialPlanProposals.Add(proposal);
        return Task.CompletedTask;
    }
}
