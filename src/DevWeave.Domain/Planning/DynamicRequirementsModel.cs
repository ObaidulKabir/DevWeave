using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Planning;

/// <summary>
/// The semantic kind of an immutable requirement-revision transition. These are deliberately more
/// precise than "edited": impact analysis needs to distinguish a clarification from a scope or
/// quality-threshold change.
/// </summary>
public enum RequirementChangeKind
{
    Added = 1,
    Clarified = 2,
    ScopeExpanded = 3,
    ScopeReduced = 4,
    ThresholdTightened = 5,
    ThresholdRelaxed = 6,
    Split = 7,
    Merged = 8,
    Withdrawn = 9,
    Replaced = 10,
    Reinstated = 11,
}

/// <summary>
/// The governed result of impact analysis for one resource reachable from a changed requirement.
/// Uncertain impact must never be represented as <see cref="Unaffected"/>.
/// </summary>
public enum ImpactDisposition
{
    Unaffected = 1,
    ReviewRequired = 2,
    Blocked = 3,
    Invalidated = 4,
    Superseded = 5,
    Orphaned = 6,
    ReverificationRequired = 7,
}

/// <summary>
/// Describes one requirement change without mutating an earlier revision. Split, merge, addition and
/// withdrawal have explicit cardinality rules so callers cannot encode them ambiguously.
/// </summary>
public sealed class RequirementChangeDescriptor
{
    public RequirementChangeKind Kind { get; }
    public IReadOnlyList<KnowledgeRevisionId> PreviousRevisions { get; }
    public IReadOnlyList<KnowledgeRevisionId> ResultRevisions { get; }
    public PrincipalId ActorId { get; }
    public string Reason { get; }
    public DateTimeOffset ValidAt { get; }
    public string IdempotencyKey { get; }

    public RequirementChangeDescriptor(
        RequirementChangeKind kind,
        IReadOnlyList<KnowledgeRevisionId> previousRevisions,
        IReadOnlyList<KnowledgeRevisionId> resultRevisions,
        PrincipalId actorId,
        string reason,
        DateTimeOffset validAt,
        string idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(previousRevisions);
        ArgumentNullException.ThrowIfNull(resultRevisions);
        ArgumentNullException.ThrowIfNull(actorId);

        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Requirement change reason is required.", nameof(reason));
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Requirement change idempotency key is required.", nameof(idempotencyKey));

        var previous = previousRevisions.ToArray();
        var result = resultRevisions.ToArray();
        EnsureUnique(previous, nameof(previousRevisions));
        EnsureUnique(result, nameof(resultRevisions));
        if (previous.Intersect(result).Any())
            throw new ArgumentException(
                "A requirement change must create new revisions; a previous revision cannot also be a result.");

        EnsureCardinality(kind, previous.Length, result.Length);

        Kind = kind;
        PreviousRevisions = previous;
        ResultRevisions = result;
        ActorId = actorId;
        Reason = reason.Trim();
        ValidAt = validAt;
        IdempotencyKey = idempotencyKey.Trim();
    }

    private static void EnsureUnique(
        IReadOnlyCollection<KnowledgeRevisionId> revisions,
        string parameter)
    {
        if (revisions.Any(revision => revision.Value == Guid.Empty))
            throw new ArgumentException("Requirement revision identifiers cannot be empty.", parameter);
        if (revisions.Distinct().Count() != revisions.Count)
            throw new ArgumentException("A requirement revision cannot be listed more than once.", parameter);
    }

    private static void EnsureCardinality(
        RequirementChangeKind kind,
        int previousCount,
        int resultCount)
    {
        var valid = kind switch
        {
            RequirementChangeKind.Added => previousCount == 0 && resultCount == 1,
            RequirementChangeKind.Split => previousCount == 1 && resultCount >= 2,
            RequirementChangeKind.Merged => previousCount >= 2 && resultCount == 1,
            RequirementChangeKind.Withdrawn => previousCount == 1 && resultCount == 0,
            _ => previousCount == 1 && resultCount == 1,
        };

        if (!valid)
            throw new ArgumentException(
                $"Requirement change '{kind}' has invalid revision cardinality " +
                $"({previousCount} previous, {resultCount} result).");
    }
}

/// <summary>
/// Snapshot of every independent precondition for an agent to continue work. Authorization or a
/// valid lease alone is insufficient: baseline, dependency and context currency are equally binding.
/// </summary>
public sealed record ExecutionEligibilitySnapshot(
    bool Authorized,
    bool BaselineCurrentOrRebased,
    bool DependenciesSatisfied,
    bool LeaseValid,
    bool FenceValid,
    bool ContextCurrent,
    bool NoUnresolvedSemanticConflict,
    bool KillSwitchAllows)
{
    public ExecutionEligibilityDecision Evaluate()
    {
        var failures = new List<string>(8);
        if (!Authorized) failures.Add("unauthorized");
        if (!BaselineCurrentOrRebased) failures.Add("baseline-stale");
        if (!DependenciesSatisfied) failures.Add("dependencies-incomplete");
        if (!LeaseValid) failures.Add("lease-invalid");
        if (!FenceValid) failures.Add("fence-invalid");
        if (!ContextCurrent) failures.Add("context-stale");
        if (!NoUnresolvedSemanticConflict) failures.Add("semantic-conflict");
        if (!KillSwitchAllows) failures.Add("kill-switch");
        return new ExecutionEligibilityDecision(failures.Count == 0, failures);
    }
}

public sealed record ExecutionEligibilityDecision(bool Allowed, IReadOnlyList<string> FailureCodes);

public enum SemanticConflictResolutionKind
{
    SelectLeft = 1,
    SelectRight = 2,
    Combine = 3,
    RejectBoth = 4,
    RequestRevision = 5,
}

public sealed record SemanticConflictId(Guid Value)
{
    public static SemanticConflictId New() => new(Guid.NewGuid());
}

/// <summary>
/// A semantic conflict preserves both competing revisions until an authorized resolution is recorded.
/// It never applies last-writer-wins.
/// </summary>
public sealed class SemanticConflict
{
    public SemanticConflictId Id { get; }
    public TenantId TenantId { get; }
    public ProjectId ProjectId { get; }
    public KnowledgeRevisionId LeftRevisionId { get; }
    public KnowledgeRevisionId RightRevisionId { get; }
    public string Reason { get; }
    public PrincipalId RecordedBy { get; }
    public DateTimeOffset RecordedAt { get; }
    public bool IsResolved { get; private set; }
    public SemanticConflictResolutionKind? Resolution { get; private set; }
    public PrincipalId? ResolvedBy { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public string? ResolutionReason { get; private set; }

    private SemanticConflict(
        SemanticConflictId id,
        TenantId tenantId,
        ProjectId projectId,
        KnowledgeRevisionId leftRevisionId,
        KnowledgeRevisionId rightRevisionId,
        string reason,
        PrincipalId recordedBy,
        DateTimeOffset recordedAt)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        LeftRevisionId = leftRevisionId;
        RightRevisionId = rightRevisionId;
        Reason = reason;
        RecordedBy = recordedBy;
        RecordedAt = recordedAt;
    }

    public static SemanticConflict Record(
        TenantId tenantId,
        ProjectId projectId,
        KnowledgeRevisionId leftRevisionId,
        KnowledgeRevisionId rightRevisionId,
        string reason,
        PrincipalId recordedBy)
    {
        ArgumentNullException.ThrowIfNull(leftRevisionId);
        ArgumentNullException.ThrowIfNull(rightRevisionId);
        ArgumentNullException.ThrowIfNull(recordedBy);
        if (leftRevisionId == rightRevisionId)
            throw new ArgumentException("A semantic conflict requires two distinct revisions.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Semantic conflict reason is required.", nameof(reason));

        return new SemanticConflict(
            SemanticConflictId.New(),
            tenantId,
            projectId,
            leftRevisionId,
            rightRevisionId,
            reason.Trim(),
            recordedBy,
            DateTimeOffset.UtcNow);
    }

    public void Resolve(
        SemanticConflictResolutionKind resolution,
        PrincipalId actorId,
        string reason)
    {
        ArgumentNullException.ThrowIfNull(actorId);
        if (!Enum.IsDefined(resolution))
            throw new ArgumentOutOfRangeException(nameof(resolution));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Semantic conflict resolution reason is required.", nameof(reason));
        if (IsResolved)
            throw new InvalidOperationException("Semantic conflict is already resolved.");

        IsResolved = true;
        Resolution = resolution;
        ResolvedBy = actorId;
        ResolvedAt = DateTimeOffset.UtcNow;
        ResolutionReason = reason.Trim();
    }

    public bool InvolvesRevision(KnowledgeRevisionId revisionId) =>
        LeftRevisionId == revisionId || RightRevisionId == revisionId;
}
