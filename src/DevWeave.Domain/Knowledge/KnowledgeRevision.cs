using DevWeave.Domain.Identity;

namespace DevWeave.Domain.Knowledge;

/// <summary>
/// An immutable, committed content revision of a <see cref="KnowledgeItem"/> (SRS REV-001, REV-002,
/// REV-008, BR-005, BR-006). Revisions are append-only: once created they never change, so a prior
/// revision remains retrievable in full and its schema version is retained forever.
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
    public string Reason { get; }
    public string? SourceReference { get; }
    public KnowledgeRevisionId? SupersededRevisionId { get; }

    private KnowledgeRevision(
        KnowledgeRevisionId id,
        TenantId tenantId,
        KnowledgeItemId knowledgeItemId,
        int revisionNumber,
        string schemaVersion,
        string content,
        PrincipalId actor,
        DateTimeOffset recordedAt,
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
        KnowledgeRevisionId? supersededRevisionId)
    {
        if (revisionNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(revisionNumber), "Revision number must be at least 1.");

        if (string.IsNullOrWhiteSpace(schemaVersion))
            throw new ArgumentException("Schema version is required.", nameof(schemaVersion));

        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content is required.", nameof(content));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required.", nameof(reason));

        return new KnowledgeRevision(
            KnowledgeRevisionId.New(),
            tenantId,
            knowledgeItemId,
            revisionNumber,
            schemaVersion,
            content,
            actor,
            DateTimeOffset.UtcNow,
            reason,
            sourceReference,
            supersededRevisionId);
    }
}