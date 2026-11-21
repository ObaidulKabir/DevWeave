using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Planning;

public sealed record RequirementChangeSetId(Guid Value)
{
    public static RequirementChangeSetId New() => new(Guid.NewGuid());
}

/// <summary>Persistence-friendly row for one change inside a <see cref="RequirementChangeSet"/>.</summary>
public sealed record RequirementChangeEntry(
    int Position,
    RequirementChangeKind Kind,
    Guid[] PreviousRevisionIds,
    Guid[] ResultRevisionIds,
    Guid ActorId,
    string Reason,
    DateTimeOffset ValidAt,
    string IdempotencyKey)
{
    public static RequirementChangeEntry From(RequirementChangeDescriptor change, int position) =>
        new(
            position,
            change.Kind,
            change.PreviousRevisions.Select(id => id.Value).ToArray(),
            change.ResultRevisions.Select(id => id.Value).ToArray(),
            change.ActorId.Value,
            change.Reason,
            change.ValidAt,
            change.IdempotencyKey);

    public RequirementChangeDescriptor ToDescriptor() =>
        new(
            Kind,
            PreviousRevisionIds.Select(id => new KnowledgeRevisionId(id)).ToArray(),
            ResultRevisionIds.Select(id => new KnowledgeRevisionId(id)).ToArray(),
            new PrincipalId(ActorId),
            Reason,
            ValidAt,
            IdempotencyKey);
}

/// <summary>
/// A governed batch of requirement-revision transitions recorded for impact analysis. The set is
/// append-only analytical input: it never mutates prior revisions.
/// </summary>
public sealed class RequirementChangeSet
{
    private readonly List<RequirementChangeEntry> _changes;

    public RequirementChangeSetId Id { get; }
    public TenantId TenantId { get; }
    public ProjectId ProjectId { get; }
    public PrincipalId RecordedBy { get; }
    public DateTimeOffset RecordedAt { get; }
    public string? CorrelationId { get; }
    public IReadOnlyList<RequirementChangeEntry> Changes => _changes;

    private RequirementChangeSet(
        RequirementChangeSetId id,
        TenantId tenantId,
        ProjectId projectId,
        PrincipalId recordedBy,
        DateTimeOffset recordedAt,
        string? correlationId)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        RecordedBy = recordedBy;
        RecordedAt = recordedAt;
        CorrelationId = correlationId;
        _changes = [];
    }

    public static RequirementChangeSet Create(
        TenantId tenantId,
        ProjectId projectId,
        PrincipalId recordedBy,
        IReadOnlyList<RequirementChangeDescriptor> changes,
        string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(recordedBy);
        ArgumentNullException.ThrowIfNull(changes);
        if (changes.Count == 0)
            throw new ArgumentException("A requirement change set requires at least one change.", nameof(changes));

        var keys = changes.Select(change => change.IdempotencyKey).ToArray();
        if (keys.Distinct(StringComparer.Ordinal).Count() != keys.Length)
            throw new ArgumentException("Requirement change idempotency keys must be unique within a set.");

        var set = new RequirementChangeSet(
            RequirementChangeSetId.New(),
            tenantId,
            projectId,
            recordedBy,
            DateTimeOffset.UtcNow,
            string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim());

        set._changes.AddRange(changes
            .Select(RequirementChangeEntry.From)
            .Select((entry, index) => entry with { Position = index }));

        return set;
    }
}
