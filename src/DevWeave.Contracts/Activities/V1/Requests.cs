using DevWeave.Domain.Activities;

namespace DevWeave.Contracts.Activities.V1;

/// <summary>A revision-pinned input consumed by an activity (SRS PRO-003).</summary>
public sealed record ActivityInputRequest(Guid ItemId, Guid RevisionId, int Position);

/// <summary>A revision-pinned output produced by an activity (SRS PRO-004).</summary>
public sealed record ActivityOutputRequest(Guid ItemId, Guid RevisionId, int Position);

/// <summary>
/// Records an append-only provenance activity (SRS PRO-001, PRO-002). The project is supplied in the
/// route; the acting principal is resolved against the tenant. Amendment types (Correction,
/// Retraction, Supersession) require a Reason and Reference (REV-008).
/// </summary>
public sealed record RecordActivityRequest(
    ActivityType Type,
    Guid Actor,
    string Purpose,
    ActivityStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    string? CorrelationId,
    string? Reason,
    Guid? Reference,
    IReadOnlyList<ActivityInputRequest> Inputs,
    IReadOnlyList<ActivityOutputRequest> Outputs);