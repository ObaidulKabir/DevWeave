using DevWeave.Domain.Identity;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Abstractions;

public interface IInitialPlanProposalRepository
{
    Task<InitialPlanProposal?> GetAsync(TenantId tenant, ProjectId project, InitialPlanProposalId id, CancellationToken ct);
    Task<InitialPlanProposal?> FindByIdempotencyAsync(TenantId tenant, ProjectId project, PrincipalId proposer, string idempotencyKey, CancellationToken ct);
    Task<IReadOnlyList<InitialPlanProposal>> ListAsync(TenantId tenant, ProjectId project, CancellationToken ct);
    Task AddAsync(InitialPlanProposal proposal, CancellationToken ct);
}
