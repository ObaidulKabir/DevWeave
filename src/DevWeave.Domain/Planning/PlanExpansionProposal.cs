using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Planning;

public sealed record PlanExpansionProposalId(Guid Value)
{
    public static PlanExpansionProposalId New() => new(Guid.NewGuid());
}

public enum PlanProposalStatus { Proposed, Accepted, Rejected }

// Agent suggestions never create approved knowledge or structural edges on their own.
public sealed class PlanExpansionProposal
{
    public PlanExpansionProposalId Id { get; private set; } = null!;
    public TenantId TenantId { get; private set; } = null!;
    public ProjectId ProjectId { get; private set; } = null!;
    public KnowledgeItemId ParentItemId { get; private set; } = null!;
    public string Key { get; private set; } = "";
    public string Title { get; private set; } = "";
    public string TypeId { get; private set; } = "";
    public string Rationale { get; private set; } = "";
    public PrincipalId ProposedBy { get; private set; } = null!;
    public DateTimeOffset ProposedAt { get; private set; }
    public PlanProposalStatus Status { get; private set; }
    public KnowledgeItemId? ResolvedItemId { get; private set; }
    public PrincipalId? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }

    private PlanExpansionProposal() { }

    public static PlanExpansionProposal Propose(TenantId tenant, ProjectId project, KnowledgeItemId parent,
        string key, string title, string typeId, string rationale, PrincipalId actor)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 100 || string.IsNullOrWhiteSpace(title) || title.Length > 200 ||
            string.IsNullOrWhiteSpace(typeId) || typeId.Length > 100 || string.IsNullOrWhiteSpace(rationale) || rationale.Length > 2000)
            throw new ArgumentException("Proposal requires a key, title, type and rationale within field limits.");
        return new PlanExpansionProposal
        {
            Id = PlanExpansionProposalId.New(), TenantId = tenant, ProjectId = project,
            ParentItemId = parent, Key = key.Trim(), Title = title.Trim(), TypeId = typeId.Trim(),
            Rationale = rationale.Trim(), ProposedBy = actor, ProposedAt = DateTimeOffset.UtcNow,
            Status = PlanProposalStatus.Proposed,
        };
    }

    public void Decide(PrincipalId reviewer, KnowledgeItemId? acceptedItem)
    {
        if (Status != PlanProposalStatus.Proposed)
            throw new InvalidOperationException("Proposal has already been decided.");
        Status = acceptedItem is null ? PlanProposalStatus.Rejected : PlanProposalStatus.Accepted;
        ResolvedItemId = acceptedItem;
        ReviewedBy = reviewer;
        ReviewedAt = DateTimeOffset.UtcNow;
    }
}
