using System.Security.Cryptography;
using System.Text;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Planning;

public sealed record InitialPlanProposalId(Guid Value)
{
    public static InitialPlanProposalId New() => new(Guid.NewGuid());
}

public enum InitialPlanProposalStatus
{
    Proposed = 0,
    Approved = 1,
    Rejected = 2,
}

/// <summary>
/// Immutable agent-authored plan bundle awaiting human review. Submission never materializes
/// Knowledge, Relations, outline, or Work; only Owner/Admin approval applies the payload.
/// </summary>
public sealed class InitialPlanProposal
{
    public InitialPlanProposalId Id { get; private set; } = null!;
    public TenantId TenantId { get; private set; } = null!;
    public ProjectId ProjectId { get; private set; } = null!;
    public string IdempotencyKey { get; private set; } = "";
    public string PayloadJson { get; private set; } = "";
    public string PayloadHash { get; private set; } = "";
    public bool RequireEmptyProject { get; private set; }
    public PrincipalId ProposedBy { get; private set; } = null!;
    public DateTimeOffset ProposedAt { get; private set; }
    public InitialPlanProposalStatus Status { get; private set; }
    public PrincipalId? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string? DecisionNote { get; private set; }
    /// <summary>JSON map of client-local refs to created server ids after approval.</summary>
    public string? ResultMappingJson { get; private set; }

    private InitialPlanProposal()
    {
    }

    public static InitialPlanProposal Propose(
        TenantId tenant,
        ProjectId project,
        string idempotencyKey,
        string payloadJson,
        bool requireEmptyProject,
        PrincipalId actor)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200)
            throw new ArgumentException("Idempotency key is required (max 200 characters).");
        if (string.IsNullOrWhiteSpace(payloadJson) || payloadJson.Length > 2_000_000)
            throw new ArgumentException("Plan payload is required and must be under 2MB.");

        return new InitialPlanProposal
        {
            Id = InitialPlanProposalId.New(),
            TenantId = tenant,
            ProjectId = project,
            IdempotencyKey = idempotencyKey.Trim(),
            PayloadJson = payloadJson,
            PayloadHash = ComputeHash(payloadJson),
            RequireEmptyProject = requireEmptyProject,
            ProposedBy = actor,
            ProposedAt = DateTimeOffset.UtcNow,
            Status = InitialPlanProposalStatus.Proposed,
        };
    }

    public void Approve(PrincipalId reviewer, string resultMappingJson, string? note = null)
    {
        Decide(reviewer, InitialPlanProposalStatus.Approved, note);
        ResultMappingJson = resultMappingJson ?? throw new ArgumentNullException(nameof(resultMappingJson));
    }

    public void Reject(PrincipalId reviewer, string? note = null) =>
        Decide(reviewer, InitialPlanProposalStatus.Rejected, note);

    private void Decide(PrincipalId reviewer, InitialPlanProposalStatus status, string? note)
    {
        if (Status != InitialPlanProposalStatus.Proposed)
            throw new InvalidOperationException("Plan import proposal has already been decided.");
        if (reviewer == ProposedBy)
            throw new InvalidOperationException("A proposer cannot review their own plan import.");
        Status = status;
        ReviewedBy = reviewer;
        ReviewedAt = DateTimeOffset.UtcNow;
        DecisionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (DecisionNote is { Length: > 2000 })
            throw new ArgumentException("Decision note must be at most 2000 characters.");
    }

    public static string ComputeHash(string payloadJson)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
