using DevWeave.Domain.Identity;

namespace DevWeave.Domain.Knowledge;

/// <summary>
/// An immutable, committed content revision of a <see cref="KnowledgeItem"/> (SRS REV-001, REV-002,
/// REV-008, BR-005, BR-006, NFR-035). Revisions are append-only: once created they never change, so a
/// prior revision remains retrievable in full and its schema version is retained forever. Optional
/// valid-time bounds sit beside platform-recorded time; legacy rows without <see cref="ValidFrom"/>
/// interpret validity from <see cref="RecordedAt"/>.
/// </summary>
public sealed class KnowledgeRevision
{
    public KnowledgeRevisionId Id { get; }
    public TenantId TenantId { get; }
    public KnowledgeItemId KnowledgeItemId { get; }
    public int RevisionNumber { get; }
    public string SchemaVersion { get; }
    public string Content { get; }
    public PrincipalId Actor { get; }
    public DateTimeOffset RecordedAt { get; }
    public DateTimeOffset? ValidFrom { get; }
    public DateTimeOffset? ValidTo { get; }
    public string Reason { get; }
    public string? SourceReference { get; }
    public KnowledgeRevisionId? SupersededRevisionId { get; }

    /// <summary>Valid-time start, defaulting to recorded time when no explicit ValidFrom was stored.</summary>
    public DateTimeOffset EffectiveValidFrom => ValidFrom ?? RecordedAt;

    private KnowledgeRevision(
        KnowledgeRevisionId id,
        TenantId tenantId,
        KnowledgeItemId knowledgeItemId,
        int revisionNumber,
        string schemaVersion,
        string content,
        PrincipalId actor,
        DateTimeOffset recordedAt,
        DateTimeOffset? validFrom,
        DateTimeOffset? validTo,
        string reason,
        string? sourceReference,
        KnowledgeRevisionId? supersededRevisionId)
    {
        Id = id;
        TenantId = tenantId;
        KnowledgeItemId = knowledgeItemId;
        RevisionNumber = revisionNumber;
        SchemaVersion = schemaVersion;
        Content = content;
        Actor = actor;
        RecordedAt = recordedAt;
        ValidFrom = validFrom;
        ValidTo = validTo;
        Reason = reason;
        SourceReference = sourceReference;
        SupersededRevisionId = supersededRevisionId;
    }

    public static KnowledgeRevision Create(
        TenantId tenantId,
        KnowledgeItemId knowledgeItemId,
        int revisionNumber,
        string schemaVersion,
        string content,
        PrincipalId actor,
        string reason,
        string? sourceReference,
        KnowledgeRevisionId? supersededRevisionId,
        DateTimeOffset? validFrom = null,
        DateTimeOffset? validTo = null)
    {
        if (revisionNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(revisionNumber), "Revision number must be at least 1.");

        if (string.IsNullOrWhiteSpace(schemaVersion))
            throw new ArgumentException("Schema version is required.", nameof(schemaVersion));

        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content is required.", nameof(content));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required.", nameof(reason));

        if (validFrom is { } from && validTo is { } to && to < from)
            throw new ArgumentException("ValidTo cannot precede ValidFrom.", nameof(validTo));

        return new KnowledgeRevision(
            KnowledgeRevisionId.New(),
            tenantId,
            knowledgeItemId,
            revisionNumber,
            schemaVersion,
            content,
            actor,
            DateTimeOffset.UtcNow,
            validFrom,
            validTo,
            reason,
            sourceReference,
            supersededRevisionId);
    }

    /// <summary>True when this revision's valid-time interval covers <paramref name="asOf"/>.</summary>
    public bool IsValidAt(DateTimeOffset asOf) =>
        EffectiveValidFrom <= asOf && (ValidTo is null || ValidTo >= asOf);
}
